using System.ComponentModel;

namespace CtrlValue.Domain.Enums;

public enum LiquidityClass
{
    [Description("LIQUID")]
    LIQUID,
    [Description("SEMI_LIQUID")]
    SEMI_LIQUID,
    [Description("ILLIQUID")]
    ILLIQUID,
    [Description("LOCKED")]
    LOCKED
}
