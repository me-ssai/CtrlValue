using System.ComponentModel;

namespace CtrlValue.Domain.Enums;

/// <summary>External price data providers supported by the background price-fetch job.</summary>
public enum PriceProviderType
{
    [Description("YAHOO_FINANCE")]
    YAHOO_FINANCE,
    [Description("COINGECKO")]
    COINGECKO,
    [Description("METALS_API")]
    METALS_API,
    [Description("ALPHA_VANTAGE")]
    ALPHA_VANTAGE,
    [Description("MANUAL")]
    MANUAL
}
