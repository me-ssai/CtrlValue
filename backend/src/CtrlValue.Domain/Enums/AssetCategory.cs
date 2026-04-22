using System.ComponentModel;

namespace CtrlValue.Domain.Enums;

public enum AssetClass
{
    [Description("CASH")]
    CASH,
    [Description("STOCK")]
    STOCK,
    [Description("ETF")]
    ETF,
    [Description("METAL")]
    METAL,
    [Description("VEHICLE")]
    VEHICLE,
    [Description("PROPERTY")]
    PROPERTY,
    [Description("SUPER")]
    SUPER,
    [Description("BUSINESS")]
    BUSINESS,
    [Description("CRYPTO")]
    CRYPTO,
    [Description("OTHER")]
    OTHER
}
