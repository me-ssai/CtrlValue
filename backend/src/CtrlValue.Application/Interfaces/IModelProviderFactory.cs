namespace CtrlValue.Application.Interfaces;

/// <summary>
/// Resolves the appropriate IModelProvider by name.
/// Allows runtime provider selection (OpenAI vs. Anthropic).
/// </summary>
public interface IModelProviderFactory
{
    /// <summary>
    /// Returns the provider for the given name (case-insensitive).
    /// Supported: "OpenAI", "Anthropic".
    /// Falls back to the default (OpenAI) if the name is not recognised.
    /// </summary>
    IModelProvider GetProvider(string? providerName = null);

    /// <summary>Returns the provider configured as the system default.</summary>
    IModelProvider GetDefaultProvider();
}
