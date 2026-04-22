using CtrlValue.Application.Interfaces;
using CtrlValue.Domain.Entities;
using CtrlValue.Domain.Enums;
using CtrlValue.Infrastructure.Data;

namespace CtrlValue.Application.Services;

/// <summary>
/// No-op IFinancialProvider for manually managed accounts.
/// Initiation/completion creates a sentinel FinancialConnection row with Provider=Manual.
/// Sync methods are no-ops since there is no external API to call.
/// </summary>
public class ManualProvider : IFinancialProvider
{
    public FinancialConnectionProvider ProviderType => FinancialConnectionProvider.Manual;

    private readonly AppDbContext _db;

    public ManualProvider(AppDbContext db)
    {
        _db = db;
    }

    public bool SupportsCountry(string countryCode) => true; // Universal fallback

    public Task<ConnectionInitResult> InitiateConnectionAsync(Guid entityId, Guid userId, string tenantId, string? mobile = null)
        => Task.FromResult(new ConnectionInitResult("none", string.Empty));

    public async Task<FinancialConnectionDto> CompleteConnectionAsync(Guid entityId, string tenantId, string callbackPayload)
    {
        var connection = new FinancialConnection
        {
            EntityId             = entityId,
            TenantId             = tenantId,
            Provider             = FinancialConnectionProvider.Manual,
            ProviderConnectionId = Guid.NewGuid().ToString(),
            EncryptedCredential  = string.Empty,
            InstitutionName      = "Manual Account",
            Country              = "AU",
            Status               = ConnectionStatus.Active
        };
        _db.FinancialConnections.Add(connection);
        await _db.SaveChangesAsync();

        return new FinancialConnectionDto(
            connection.Id, connection.EntityId, "Manual", connection.ProviderConnectionId,
            "Manual Account", null, "Active", null, connection.Country,
            null, null, 0);
    }

    public Task SyncAccountsAsync(FinancialConnection connection) => Task.CompletedTask;
    public Task<int> SyncTransactionsAsync(FinancialConnection connection, DateTime startDate, DateTime endDate) => Task.FromResult(0);
    public Task RevokeConnectionAsync(FinancialConnection connection) => Task.CompletedTask;
}
