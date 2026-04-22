using Microsoft.EntityFrameworkCore;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain.Entities;
using CtrlValue.Domain.Enums;
using CtrlValue.Infrastructure.Data;

namespace CtrlValue.Application.Services;

/// <summary>
/// IFinancialProvider implementation for CSV file imports.
/// Models a CSV upload as a true persistent connection so re-imports can
/// be associated with the same institution over time.
///
/// callbackPayload for CompleteConnectionAsync is a JSON blob:
///   { "institutionName": "...", "country": "AU" }
///
/// The actual file staging is handled by the existing OFX/QIF/CSV import
/// pipeline — CsvProvider just creates and manages the FinancialConnection record.
/// </summary>
public class CsvProvider : IFinancialProvider
{
    public FinancialConnectionProvider ProviderType => FinancialConnectionProvider.Csv;

    private readonly AppDbContext _db;

    public CsvProvider(AppDbContext db)
    {
        _db = db;
    }

    public bool SupportsCountry(string countryCode) => true;

    public Task<ConnectionInitResult> InitiateConnectionAsync(Guid entityId, Guid userId, string tenantId, string? mobile = null)
        => Task.FromResult(new ConnectionInitResult("none", string.Empty));

    public async Task<FinancialConnectionDto> CompleteConnectionAsync(Guid entityId, string tenantId, string callbackPayload)
    {
        // Parse the payload
        string institutionName = "CSV Import";
        string country = "AU";

        if (!string.IsNullOrWhiteSpace(callbackPayload))
        {
            try
            {
                var doc = System.Text.Json.JsonDocument.Parse(callbackPayload);
                if (doc.RootElement.TryGetProperty("institutionName", out var nameEl))
                    institutionName = nameEl.GetString() ?? institutionName;
                if (doc.RootElement.TryGetProperty("country", out var countryEl))
                    country = countryEl.GetString() ?? country;
            }
            catch { /* use defaults */ }
        }

        // Check if a CSV connection for this institution already exists
        var existing = await _db.FinancialConnections
            .FirstOrDefaultAsync(c => c.EntityId == entityId
                && c.Provider == FinancialConnectionProvider.Csv
                && c.InstitutionName == institutionName);

        if (existing != null)
        {
            var existingCount = await _db.ConnectedAccounts.CountAsync(a => a.ConnectionId == existing.Id);
            return new FinancialConnectionDto(
                existing.Id, existing.EntityId, "Csv", existing.ProviderConnectionId,
                existing.InstitutionName, null, existing.Status.ToString(), null,
                existing.Country, existing.LastSyncedAt, null, existingCount);
        }

        var connection = new FinancialConnection
        {
            EntityId             = entityId,
            TenantId             = tenantId,
            Provider             = FinancialConnectionProvider.Csv,
            ProviderConnectionId = Guid.NewGuid().ToString(),
            EncryptedCredential  = string.Empty,
            InstitutionName      = institutionName,
            Country              = country,
            Status               = ConnectionStatus.Active
        };
        _db.FinancialConnections.Add(connection);
        await _db.SaveChangesAsync();

        return new FinancialConnectionDto(
            connection.Id, connection.EntityId, "Csv", connection.ProviderConnectionId,
            institutionName, null, "Active", null, country,
            null, null, 0);
    }

    // CSV connections don't have live balances — no-op.
    public Task SyncAccountsAsync(FinancialConnection connection) => Task.CompletedTask;
    public Task<int> SyncTransactionsAsync(FinancialConnection connection, DateTime startDate, DateTime endDate) => Task.FromResult(0);
    public Task RevokeConnectionAsync(FinancialConnection connection) => Task.CompletedTask;
}
