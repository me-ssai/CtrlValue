namespace CtrlValue.Domain.Entities;

/// <summary>
/// Platform-level (super-admin managed) API key for price providers.
/// Used as Tier 2 fallback when an entity has no personal API key configured.
/// API keys are AES-256 encrypted at rest using the same IEncryptionService as EntityIntegration.
/// One row per integration type (ALPHA_VANTAGE, METALS_API, COINGECKO).
/// </summary>
public class PlatformIntegration : BaseEntity
{
    /// <summary>Integration type — e.g. "ALPHA_VANTAGE", "METALS_API", "COINGECKO".</summary>
    public string IntegrationType { get; set; } = string.Empty;

    /// <summary>AES-256 encrypted API key.</summary>
    public string? ApiKey { get; set; }

    /// <summary>When false the key is stored but NOT used as a fallback.</summary>
    public bool IsEnabled { get; set; } = false;

    /// <summary>When this key was last used by the price-fetch job or a search request.</summary>
    public DateTime? LastUsedAt { get; set; }
}
