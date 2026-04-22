using Microsoft.Extensions.DependencyInjection;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain.Enums;

namespace CtrlValue.Application.Services;

/// <summary>
/// Resolves the correct IFinancialProvider based on country code or explicit provider type.
/// Registered as Scoped — lives within the request scope, so resolving other scoped services
/// (CsvProvider, ManualProvider, AppDbContext, etc.) via the injected IServiceProvider is safe.
///
/// All countries default to Manual (no external API).
/// </summary>
public class FinancialProviderFactory : IFinancialProviderFactory
{
    private readonly IServiceProvider _services;

    public FinancialProviderFactory(IServiceProvider services)
    {
        _services = services;
    }

    public IFinancialProvider Resolve(string countryCode)
        => Resolve(FinancialConnectionProvider.Manual);

    public IFinancialProvider Resolve(FinancialConnectionProvider providerType) => providerType switch
    {
        FinancialConnectionProvider.Csv    => _services.GetRequiredService<CsvProvider>(),
        FinancialConnectionProvider.Manual => _services.GetRequiredService<ManualProvider>(),
        _ => _services.GetRequiredService<ManualProvider>()
    };

    public IFinancialProvider? TryFallback(FinancialConnectionProvider failedProvider, string countryCode)
        => Resolve(FinancialConnectionProvider.Manual);
}
