using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain.Entities;
using CtrlValue.Infrastructure.Data;

namespace CtrlValue.Application.Services;

public class EntityIntegrationService : IEntityIntegrationService
{
    private readonly AppDbContext _db;
    private readonly IEncryptionService _encryption;
    private readonly IConfiguration _config;

    // Config key prefix — Tier 3 fallback from appsettings
    private static readonly Dictionary<string, string> PlatformKeyPaths = new()
    {
        ["ALPHA_VANTAGE"] = "AlphaVantage:ApiKey",
        ["METALS_API"]    = "MetalsApi:ApiKey",
        ["OPENAI"]        = "Agent:OpenAI:ApiKey",
        ["ANTHROPIC"]     = "Agent:Anthropic:ApiKey",
    };

    public EntityIntegrationService(AppDbContext db, IEncryptionService encryption, IConfiguration config)
    {
        _db         = db;
        _encryption = encryption;
        _config     = config;
    }

    // ── Entity-level integration CRUD ──────────────────────────────────────────

    public async Task<List<EntityIntegrationDto>> GetIntegrationsAsync(Guid entityId)
    {
        var rows = await _db.EntityIntegrations
            .Where(ei => ei.EntityId == entityId)
            .ToListAsync();

        return rows.Select(ToDto).ToList();
    }

    public async Task<EntityIntegrationDto?> GetIntegrationAsync(Guid entityId, string integrationType)
    {
        var row = await _db.EntityIntegrations
            .FirstOrDefaultAsync(ei => ei.EntityId == entityId && ei.IntegrationType == integrationType);

        return row == null ? null : ToDto(row);
    }

    public async Task<EntityIntegrationDto> UpsertIntegrationAsync(
        Guid entityId, string tenantId, string integrationType, string? apiKey, bool isEnabled)
    {
        var existing = await _db.EntityIntegrations
            .FirstOrDefaultAsync(ei => ei.EntityId == entityId && ei.IntegrationType == integrationType);

        if (existing == null)
        {
            existing = new EntityIntegration
            {
                EntityId        = entityId,
                TenantId        = tenantId,
                IntegrationType = integrationType,
            };
            _db.EntityIntegrations.Add(existing);
        }

        existing.IsEnabled = isEnabled;

        // Only update the stored key if the caller supplied one
        if (apiKey != null)
            existing.ApiKey = _encryption.Encrypt(apiKey);

        await _db.SaveChangesAsync();
        return ToDto(existing);
    }

    public async Task DeleteIntegrationAsync(Guid entityId, string integrationType)
    {
        var existing = await _db.EntityIntegrations
            .FirstOrDefaultAsync(ei => ei.EntityId == entityId && ei.IntegrationType == integrationType);

        if (existing == null) return;

        existing.IsEnabled = false;
        existing.ApiKey    = null;
        await _db.SaveChangesAsync();
    }

    // ── 3-tier API key resolution ──────────────────────────────────────────────

    public async Task<string?> GetEffectiveApiKeyAsync(Guid entityId, string integrationType)
    {
        // Tier 1: entity's own encrypted key
        var entityRow = await _db.EntityIntegrations
            .FirstOrDefaultAsync(ei => ei.EntityId == entityId
                                    && ei.IntegrationType == integrationType
                                    && ei.IsEnabled);

        if (entityRow?.ApiKey != null)
            return _encryption.Decrypt(entityRow.ApiKey);

        // Tier 2: platform/admin key from DB (super-admin managed)
        var platformRow = await _db.PlatformIntegrations
            .FirstOrDefaultAsync(pi => pi.IntegrationType == integrationType
                                    && pi.IsEnabled
                                    && !pi.IsDeleted);

        if (platformRow?.ApiKey != null)
        {
            platformRow.LastUsedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return _encryption.Decrypt(platformRow.ApiKey);
        }

        // Tier 3: hardcoded config (appsettings fallback)
        if (PlatformKeyPaths.TryGetValue(integrationType, out var configPath))
            return _config[configPath]; // null if not configured

        return null;
    }

    // ── Platform (super-admin) integration management ──────────────────────────

    public async Task<List<PlatformIntegrationDto>> GetPlatformIntegrationsAsync()
    {
        var rows = await _db.PlatformIntegrations
            .Where(pi => !pi.IsDeleted)
            .ToListAsync();

        return rows.Select(ToPlatformDto).ToList();
    }

    public async Task<PlatformIntegrationDto> UpsertPlatformIntegrationAsync(
        string integrationType, string? apiKey, bool isEnabled)
    {
        var existing = await _db.PlatformIntegrations
            .FirstOrDefaultAsync(pi => pi.IntegrationType == integrationType && !pi.IsDeleted);

        if (existing == null)
        {
            existing = new PlatformIntegration
            {
                IntegrationType = integrationType,
            };
            _db.PlatformIntegrations.Add(existing);
        }

        existing.IsEnabled = isEnabled;

        if (apiKey != null)
            existing.ApiKey = _encryption.Encrypt(apiKey);

        await _db.SaveChangesAsync();
        return ToPlatformDto(existing);
    }

    public async Task DeletePlatformIntegrationAsync(string integrationType)
    {
        var existing = await _db.PlatformIntegrations
            .FirstOrDefaultAsync(pi => pi.IntegrationType == integrationType && !pi.IsDeleted);

        if (existing == null) return;

        existing.IsEnabled = false;
        existing.ApiKey    = null;
        await _db.SaveChangesAsync();
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static EntityIntegrationDto ToDto(EntityIntegration ei) => new(
        ei.Id,
        ei.EntityId,
        ei.IntegrationType,
        ei.IsEnabled,
        HasApiKey: ei.ApiKey != null,
        ei.LastSyncedAt,
        ei.CreatedAt
    );

    private static PlatformIntegrationDto ToPlatformDto(PlatformIntegration pi) => new(
        pi.Id,
        pi.IntegrationType,
        pi.IsEnabled,
        HasApiKey: pi.ApiKey != null,
        pi.LastUsedAt,
        pi.CreatedAt
    );
}
