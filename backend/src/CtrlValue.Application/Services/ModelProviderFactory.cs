using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using CtrlValue.Application.Interfaces;

namespace CtrlValue.Application.Services;

/// <summary>
/// Resolves the correct IModelProvider by name.
/// The default provider is controlled by Agent:DefaultProvider in configuration.
/// </summary>
public class ModelProviderFactory : IModelProviderFactory
{
    private readonly IEnumerable<IModelProvider> _providers;
    private readonly string _defaultProviderName;
    private readonly ILogger<ModelProviderFactory> _logger;

    public ModelProviderFactory(
        IEnumerable<IModelProvider> providers,
        IConfiguration config,
        ILogger<ModelProviderFactory> logger)
    {
        _providers = providers;
        _defaultProviderName = config["Agent:DefaultProvider"] ?? "OpenAI";
        _logger = logger;
    }

    public IModelProvider GetProvider(string? providerName = null)
    {
        var name = providerName ?? _defaultProviderName;

        var provider = _providers.FirstOrDefault(p =>
            p.ProviderName.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (provider == null)
        {
            _logger.LogWarning(
                "[ModelProviderFactory] Provider '{Name}' not found — falling back to default.",
                name);
            provider = GetDefaultProvider();
        }

        return provider;
    }

    public IModelProvider GetDefaultProvider()
    {
        var provider = _providers.FirstOrDefault(p =>
            p.ProviderName.Equals(_defaultProviderName, StringComparison.OrdinalIgnoreCase))
            ?? _providers.First(); // Last resort: first registered

        return provider;
    }
}
