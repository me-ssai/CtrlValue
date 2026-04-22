using System.ComponentModel;

namespace CtrlValue.Domain.Enums;

public enum InstrumentType
{
    [Description("STOCK")]
    STOCK,
    [Description("BOND")]
    BOND,
    [Description("ETF")]
    ETF,
    [Description("METAL")]
    METAL,
    [Description("CRYPTO")]
    CRYPTO,
    [Description("FUND")]
    FUND,
    [Description("OTHER")]
    OTHER
}
