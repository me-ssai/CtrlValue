namespace CtrlValue.Application.Interfaces;

public interface IEntityIntegrationService
{
    /// <summary>Returns all integration configs for the entity. API keys are redacted (null in response).</summary>
    Task<List<EntityIntegrationDto>> GetIntegrationsAsync(Guid entityId);

    /// <summary>Returns one integration config. API key is redacted.</summary>
    Task<EntityIntegrationDto?> GetIntegrationAsync(Guid entityId, string integrationType);

    /// <summary>Creates or updates an integration config. Encrypts the API key before storing.</summary>
    Task<EntityIntegrationDto> UpsertIntegrationAsync(Guid entityId, string tenantId, string integrationType, string? apiKey, bool isEnabled);

    /// <summary>Disables the integration and clears the stored API key.</summary>
    Task DeleteIntegrationAsync(Guid entityId, string integrationType);

    /// <summary>
    /// Internal: returns the decrypted API key for a given entity and integration type.
    /// Resolution order:
    ///   1. Entity's own encrypted key (EntityIntegration)
    ///   2. Platform-level admin key (PlatformIntegration table — super admin managed)
    ///   3. Platform-level key from IConfiguration (appsettings fallback)
    /// Returns null if no key is available anywhere.
    /// </summary>
    Task<string?> GetEffectiveApiKeyAsync(Guid entityId, string integrationType);

    // ── Platform (super-admin) integration management ──────────────────────

    /// <summary>Returns all platform-level integration configs. API keys are masked.</summary>
    Task<List<PlatformIntegrationDto>> GetPlatformIntegrationsAsync();

    /// <summary>Creates or updates a platform-level integration key.</summary>
    Task<PlatformIntegrationDto> UpsertPlatformIntegrationAsync(string integrationType, string? apiKey, bool isEnabled);

    /// <summary>Disables and removes the platform-level key for the given integration type.</summary>
    Task DeletePlatformIntegrationAsync(string integrationType);
}

public record EntityIntegrationDto(
    Guid Id,
    Guid EntityId,
    string IntegrationType,
    bool IsEnabled,
    bool HasApiKey,
    DateTime? LastSyncedAt,
    DateTime CreatedAt
);

public record PlatformIntegrationDto(
    Guid Id,
    string IntegrationType,
    bool IsEnabled,
    bool HasApiKey,
    DateTime? LastUsedAt,
    DateTime CreatedAt
);
