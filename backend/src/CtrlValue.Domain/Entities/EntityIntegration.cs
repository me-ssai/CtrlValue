namespace CtrlValue.Domain.Entities;

/// <summary>
/// Stores per-entity configuration for external market data integrations
/// (Alpha Vantage, Metals API). API keys are AES-256 encrypted at rest.
/// One row per entity per integration type.
/// </summary>
public class EntityIntegration : BaseEntity
{
    public Guid EntityId { get; set; }

    /// <summary>Integration type identifier — e.g. "ALPHA_VANTAGE", "METALS_API".</summary>
    public string IntegrationType { get; set; } = string.Empty;

    /// <summary>AES-256 encrypted API key. Null if using the platform-level fallback key.</summary>
    public string? ApiKey { get; set; }

    public bool IsEnabled { get; set; } = false;

    /// <summary>Reserved for type-specific JSON config (e.g. base currency, rate limits).</summary>
    public string? Settings { get; set; }

    public DateTime? LastSyncedAt { get; set; }

    // Navigation
    public Entity Entity { get; set; } = null!;
}
