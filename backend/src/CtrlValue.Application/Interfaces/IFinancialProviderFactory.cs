using CtrlValue.Domain.Enums;

namespace CtrlValue.Application.Interfaces;

/// <summary>
/// Resolves the correct IFinancialProvider for a given country or explicit provider type.
/// Registered as a singleton in DI. Implementations are resolved from the IServiceProvider.
/// </summary>
public interface IFinancialProviderFactory
{
    /// <summary>
    /// Resolves the preferred provider for the given ISO-3166-1 country code.
    /// AU, NZ → Basiq
    /// US, CA, GB, IE, FR, ES, NL, DE → Plaid
    /// All others → Manual (no external API)
    /// </summary>
    IFinancialProvider Resolve(string countryCode);

    /// <summary>Resolves a provider by explicit type (used for syncing existing connections).</summary>
    IFinancialProvider Resolve(FinancialConnectionProvider providerType);

    /// <summary>
    /// Attempts to find a fallback provider when the primary fails.
    /// Returns null if no fallback is available (e.g., Manual is the last resort).
    /// </summary>
    IFinancialProvider? TryFallback(FinancialConnectionProvider failedProvider, string countryCode);
}
