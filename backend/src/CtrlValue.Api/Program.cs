using System.Text;
using System.Threading.RateLimiting;
using Azure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using CtrlValue.Api.Infrastructure;
using CtrlValue.Application.Interfaces;
using CtrlValue.Application.Services;
using CtrlValue.Domain;
using CtrlValue.Infrastructure.Data;
using CtrlValue.Infrastructure.Data.Seeders;
using CtrlValue.Api.Jobs;

var builder = WebApplication.CreateBuilder(args);

// ── Azure Key Vault (optional — set KeyVault:VaultUri to enable) ──
var keyVaultUri = builder.Configuration["KeyVault:VaultUri"];
if (!string.IsNullOrWhiteSpace(keyVaultUri))
{
    try { builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUri), new DefaultAzureCredential()); }
    catch (Exception ex) { Console.WriteLine($"Warning: Could not connect to Azure Key Vault: {ex.Message}"); }
}

// ── Database ──
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Authentication (Custom JWT) ──
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? throw new InvalidOperationException("JWT Secret not configured");
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ClockSkew = TimeSpan.Zero
    };

    // Accept JWT from httpOnly cookie (preferred) or Authorization header (Swagger / dev tools)
    options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
    {
        OnMessageReceived = ctx =>
        {
            if (ctx.Request.Cookies.TryGetValue("access_token", out var cookieToken))
                ctx.Token = cookieToken;
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SuperAdmin", policy =>
        policy.RequireClaim(System.Security.Claims.ClaimTypes.Role, "SuperAdmin"));

    // SiteAdmin policy accepts both SiteAdmin and SuperAdmin (superadmins can do everything)
    options.AddPolicy("SiteAdmin", policy =>
        policy.RequireClaim(System.Security.Claims.ClaimTypes.Role, "SiteAdmin", "SuperAdmin"));
});

// ── Rate Limiting ──
// In the Testing environment limits are set very high so integration tests never hit 429.
var isTesting = builder.Environment.IsEnvironment("Testing");
builder.Services.AddRateLimiter(options =>
{
    // Global baseline: 200 requests/min per IP (unlimited in tests)
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter($"global:{ip}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = isTesting ? 100_000 : 200,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });

    // Auth policy: 10 attempts per 15 min per IP — applied to sensitive auth endpoints
    options.AddPolicy<string>("auth", context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter($"auth:{ip}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = isTesting ? 100_000 : 10,
            Window = TimeSpan.FromMinutes(15),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });

    // Demo policy: 50 requests/min per IP — stricter because demo is public-facing
    options.AddPolicy<string>("demo", context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter($"demo:{ip}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = isTesting ? 100_000 : 50,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// ── Demo Tenant ──
builder.Services.AddScoped<DemoRequestContext>();

// ── Application Services ──
builder.Services.AddScoped<IAuditService, HttpAuditService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IEntityService, EntityService>();
builder.Services.AddScoped<IDefaultCategorySeeder, DefaultCategorySeeder>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<ICategoryKeywordRuleService, CategoryKeywordRuleService>();
builder.Services.AddScoped<IAccountKeywordRuleService, AccountKeywordRuleService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IInstrumentService, InstrumentService>();
builder.Services.AddScoped<IPositionService, PositionService>();
builder.Services.AddScoped<IPriceHistoryService, PriceHistoryService>();
builder.Services.AddScoped<IValuationService, ValuationService>();
builder.Services.AddScoped<IDepreciationScheduleService, DepreciationScheduleService>();
builder.Services.AddScoped<IBudgetService, BudgetService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IQifImportService, QifImportService>();
builder.Services.AddScoped<IOfxImportService, OfxImportService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<ILoanService, LoanService>();
builder.Services.AddScoped<IPropertyService, PropertyService>();
builder.Services.AddScoped<IUserDeletionService, UserDeletionService>();
builder.Services.AddHostedService<DeletionSchedulerJob>();

// ── Integration services ──
builder.Services.AddSingleton<IEncryptionService, EncryptionService>();
builder.Services.AddScoped<IEntityIntegrationService, EntityIntegrationService>();
builder.Services.AddScoped<AlphaVantageService>();
builder.Services.AddScoped<MetalsPriceService>();
builder.Services.AddScoped<CoinGeckoService>();
builder.Services.AddScoped<YahooFinanceService>();
builder.Services.AddSingleton<YahooHttpClient>();
builder.Services.AddSingleton<PriceFetchJob>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<PriceFetchJob>());
builder.Services.AddHostedService<DefaultInstrumentSeeder>();
builder.Services.AddHostedService<DemoDataSeeder>();

// ── Financial Connectivity Layer ──
builder.Services.AddScoped<ManualProvider>();
builder.Services.AddScoped<CsvProvider>();
builder.Services.AddScoped<IFinancialProviderFactory, FinancialProviderFactory>();
builder.Services.AddScoped<IConnectionService, ConnectionService>();
builder.Services.AddHostedService<ConnectionSyncJob>();

// ── Transaction Intelligence ──
builder.Services.AddScoped<ITransactionIntelligenceService, TransactionIntelligenceService>();

// ── Alpha Vantage HTTP client ──
builder.Services.AddHttpClient("AlphaVantage", client =>
{
    client.BaseAddress = new Uri("https://www.alphavantage.co/query");
    client.Timeout     = TimeSpan.FromSeconds(30);
});

// ── Metals Price API HTTP client (metalpriceapi.com — free, no credit card) ──
builder.Services.AddHttpClient("MetalsApi", client =>
{
    client.BaseAddress = new Uri("https://api.metalpriceapi.com/");
    client.Timeout     = TimeSpan.FromSeconds(30);
});

// ── CoinGecko HTTP client ──
builder.Services.AddHttpClient("CoinGecko", client =>
{
    client.BaseAddress = new Uri("https://api.coingecko.com/api/v3/");
    client.Timeout     = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// Yahoo Finance uses YahooHttpClient (singleton) which manages its own
// HttpClient with CookieContainer for crumb/session auth — no named client needed.

// ── CtrlValue Agent services ──
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IAgentFeatureFlagService, AgentFeatureFlagService>();
builder.Services.AddScoped<IAgentContextBuilderService, AgentContextBuilderService>();
builder.Services.AddScoped<IAgentInsightService, AgentInsightService>();
builder.Services.AddScoped<IAgentOrchestratorService, AgentOrchestratorService>();
builder.Services.AddScoped<IAgentWebResearchService, AgentWebResearchService>();
builder.Services.AddScoped<IAgentAuditService, AgentAuditService>();
builder.Services.AddScoped<IAgentScenarioService, AgentScenarioService>();
builder.Services.AddScoped<IAgentSavingsHistoryService, AgentSavingsHistoryService>();
builder.Services.AddScoped<IAgentSettingService, AgentSettingService>();
builder.Services.AddScoped<IAgentDigestService, AgentDigestService>();
builder.Services.AddHostedService<AgentAlertJob>();
builder.Services.AddHostedService<AgentDigestJob>();

// Register providers as named/typed HttpClients — both implement IModelProvider
// The factory (IModelProviderFactory) selects between them at runtime
builder.Services.AddHttpClient<OpenAiProvider>(client =>
{
    var baseUrl = builder.Configuration["Agent:OpenAI:BaseUrl"] ?? "https://api.openai.com";
    var apiKey  = builder.Configuration["Agent:OpenAI:ApiKey"]  ?? string.Empty;

    client.BaseAddress = new Uri(baseUrl);
    client.Timeout     = TimeSpan.FromSeconds(120);

    if (!string.IsNullOrWhiteSpace(apiKey))
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
});

builder.Services.AddHttpClient<ClaudeProvider>(client =>
{
    var baseUrl = builder.Configuration["Agent:Anthropic:BaseUrl"] ?? "https://api.anthropic.com";
    var apiKey  = builder.Configuration["Agent:Anthropic:ApiKey"] ?? string.Empty;

    client.BaseAddress = new Uri(baseUrl);
    client.Timeout     = TimeSpan.FromSeconds(120);

    if (!string.IsNullOrWhiteSpace(apiKey))
        client.DefaultRequestHeaders.Add("x-api-key", apiKey);
    client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
});

// AddHttpClient<T> already registers each provider as a transient typed client with the
// configured HttpClient (BaseAddress, headers). Do NOT add duplicate AddScoped<T> registrations
// here — they would override the typed client registrations with a plain DI resolution that
// receives the default, unconfigured HttpClient (no BaseAddress), causing runtime failures.
//
// The factory resolves both providers via GetRequiredService which uses the AddHttpClient
// registrations and receives properly configured HttpClient instances.
builder.Services.AddScoped<IModelProviderFactory>(sp =>
{
    var providers = new List<IModelProvider>
    {
        sp.GetRequiredService<OpenAiProvider>(),
        sp.GetRequiredService<ClaudeProvider>()
    };
    var config = sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
    var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ModelProviderFactory>>();
    return new ModelProviderFactory(providers, config, logger);
});

// ── ProjectZAI (local LLM proxy — OpenAI-compatible) ──
builder.Services.AddHttpClient<IAiCategorizationService, AiCategorizationService>(client =>
{
    var baseUrl = builder.Configuration["ProjectZAI:BaseUrl"] ?? "http://127.0.0.1:1234";
    var apiKey  = builder.Configuration["ProjectZAI:ApiKey"]  ?? string.Empty;
    var timeout = int.TryParse(builder.Configuration["ProjectZAI:TimeoutSeconds"], out var t) ? t : 60;

    client.BaseAddress = new Uri(baseUrl);
    client.Timeout     = TimeSpan.FromSeconds(timeout);

    if (!string.IsNullOrWhiteSpace(apiKey))
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
});

// ── Infrastructure ──
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<TenantContext>();

// builder.Services.AddScoped<IFinanceService, FinanceService>(); // Temporarily disabled during schema migration


// ── Controllers ──
builder.Services.AddControllers(options =>
     {
         options.Filters.Add<BlockDemoWritesFilter>();
     })
     .AddJsonOptions(options =>
     {
         options.JsonSerializerOptions.Converters
             .Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
     });

// ── Swagger / OpenAPI ──
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Project Z API",
        Version = "v1",
        Description = "Personal Finance & Wealth Operating System"
    });

    // Bearer token security scheme
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ── CORS (allow Angular dev server & production) ──
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        // Support both array format (Cors__AllowedOrigins__0) and plain string (Cors__AllowedOrigins)
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                            ?? builder.Configuration["Cors:AllowedOrigins"]?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            ?? new[] { "http://localhost:4200" };

        Console.WriteLine($"CORS AllowedOrigins: {string.Join(", ", allowedOrigins)}");

        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// ── Middleware Pipeline ──

// Global error handling
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        // UseExceptionHandler calls Response.Clear() internally, which strips CORS headers
        // that UseCors already set. Re-apply them here so cross-origin clients can read errors.
        var origin = context.Request.Headers.Origin.FirstOrDefault();
        if (!string.IsNullOrEmpty(origin))
        {
            var config         = context.RequestServices.GetRequiredService<IConfiguration>();
            var allowedOrigins = config.GetSection("Cors:AllowedOrigins").Get<string[]>()
                              ?? config["Cors:AllowedOrigins"]?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                              ?? Array.Empty<string>();

            if (allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
            {
                context.Response.Headers["Access-Control-Allow-Origin"]      = origin;
                context.Response.Headers["Access-Control-Allow-Credentials"] = "true";
            }
        }

        context.Response.ContentType = "application/json";
        var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        var exception = feature?.Error;

        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

        var (statusCode, message) = exception switch
        {
            KeyNotFoundException             => (404, exception.Message),
            ArgumentException                => (400, exception.Message),
            UnauthorizedAccessException      => (401, exception.Message),
            Microsoft.IdentityModel.Tokens.SecurityTokenException => (401, exception.Message),
            InvalidOperationException        => (400, exception.Message),
            _                                => (500, "An unexpected error occurred.")
        };

        if (statusCode == 500)
            logger.LogError(exception, "Unhandled exception: {Path}", context.Request.Path);
        else
            logger.LogWarning(exception, "Handled exception ({StatusCode}): {Path}", statusCode, context.Request.Path);

        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(new { error = message, statusCode });
    });
});

// ── Secure Response Headers ──
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Frame-Options"]           = "DENY";
    context.Response.Headers["X-Content-Type-Options"]    = "nosniff";
    context.Response.Headers["X-XSS-Protection"]          = "1; mode=block";
    context.Response.Headers["Referrer-Policy"]            = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"]         = "camera=(), microphone=(), geolocation=(), payment=()";
    context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
    await next();
});

if (builder.Environment.IsDevelopment())
{
    // ── Swagger / OpenAPI (Enabled in all environments for now) ──
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Project Z API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();

app.UseRouting();
app.UseMiddleware<DemoRequestMiddleware>();
app.UseCors("AllowAngular");

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// ── Root endpoint for health check ──
app.MapGet("/", () => Results.Ok(new { message = "Project Z API is running", environment = app.Environment.EnvironmentName, swagger = "/swagger" }));

// ── Database Migrations ──
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}
catch (Exception ex)
{
    // Log migration errors but try to keep the app running for diagnostics
    Console.WriteLine($"Error during database migration: {ex.Message}");
    if (ex.InnerException != null)
    {
        Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
    }
}

app.Run();

// Required for WebApplicationFactory<Program> in integration tests
public partial class Program { }
