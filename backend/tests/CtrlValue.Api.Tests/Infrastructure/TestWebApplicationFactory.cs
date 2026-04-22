using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using CtrlValue.Application.Interfaces;
using CtrlValue.Infrastructure.Data;

namespace CtrlValue.Api.Tests.Infrastructure;

/// <summary>
/// Shared WebApplicationFactory that replaces the PostgreSQL database with an EF Core
/// in-memory database so integration tests run without an external DB.
/// Each test class that uses <see cref="IClassFixture{TestWebApplicationFactory}"/>
/// gets the same factory instance; use <see cref="ResetDatabaseAsync"/> between
/// test methods when isolation is needed.
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    // Unique DB name per factory instance so parallel test classes don't share state.
    private readonly string _dbName = $"TestDb_{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove the real Npgsql DbContext registration.
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();

            // Register EF Core in-memory DB instead.
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));

            // Remove all background hosted services — they depend on external services
            // (Encryption:Key, external APIs, etc.) that are not configured in tests
            // and can cause 500 errors or interfere with request handling.
            services.RemoveAll<IHostedService>();

            // Replace the real email service with a no-op so Register/ForgotPassword
            // don't attempt SMTP connections during tests.
            services.RemoveAll<IEmailService>();
            services.AddSingleton<IEmailService, NoOpEmailService>();

            // Build a temporary provider to seed the schema & test data.
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
            TestDataSeeder.Seed(db);
        });

        // Override JWT config so generated tokens validate correctly in tests.
        builder.UseSetting("Jwt:Secret",         "test-secret-key-that-is-long-enough-for-hmac-256");
        builder.UseSetting("Jwt:Issuer",         "CtrlValue.Tests");
        builder.UseSetting("Jwt:Audience",       "CtrlValue.Tests");
        builder.UseSetting("Jwt:ExpiryMinutes",  "60");
        builder.UseSetting("Demo:Enabled",         "true");
        // Override the allowed origins array from appsettings.json.
        // UseSetting sets both the scalar key (for Split fallback) and index 0 (for array binding).
        builder.UseSetting("Demo:AllowedOrigins",   "https://demo.test");
        builder.UseSetting("Demo:AllowedOrigins:0", "https://demo.test");
        builder.UseSetting("Demo:TokenExpiryMinutes", "60");
        builder.UseSetting("KeyVault:Enabled",   "false");

        // Provide a dummy encryption key so any remaining service that instantiates
        // EncryptionService does not throw on startup.
        builder.UseSetting("Encryption:Key", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=");
    }

    /// <summary>
    /// Creates an <see cref="HttpClient"/> that is pre-authenticated as the given user.
    /// The JWT cookie is injected directly so no login round-trip is needed.
    /// </summary>
    public HttpClient CreateAuthenticatedClient(TestUser user)
    {
        var client = CreateClient();
        var token  = JwtHelper.GenerateToken(user, "test-secret-key-that-is-long-enough-for-hmac-256");
        client.DefaultRequestHeaders.Add("Cookie", $"access_token={token}");
        if (user.EntityId.HasValue)
            client.DefaultRequestHeaders.Add("X-Entity-Id", user.EntityId.Value.ToString());
        return client;
    }

    /// <summary>Resets all data in the in-memory DB back to the seed state.</summary>
    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();
        TestDataSeeder.Seed(db);
        await Task.CompletedTask;
    }
}

/// <summary>No-op email service for integration tests — prevents SMTP connections.</summary>
internal sealed class NoOpEmailService : IEmailService
{
    public Task SendEmailVerificationAsync(string toEmail, string firstName, string verificationToken, string tenantId) => Task.CompletedTask;
    public Task Send2FAOtpAsync(string toEmail, string firstName, string otp) => Task.CompletedTask;
    public Task SendPasswordResetAsync(string toEmail, string firstName, string resetToken) => Task.CompletedTask;
    public Task SendInviteEmailAsync(string toEmail, string inviteToken) => Task.CompletedTask;
}
