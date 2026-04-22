using System.ComponentModel;

namespace CtrlValue.Domain.Enums;

/// <summary>Units of measurement for physical metals. UNIT is the default for non-metal instruments (stocks, ETFs, crypto).</summary>
public enum MetalUnit
{
    [Description("UNIT")]
    UNIT,
    [Description("TROY_OZ")]
    TROY_OZ,
    [Description("GRAM")]
    GRAM,
    [Description("KILOGRAM")]
    KILOGRAM,
    [Description("TOLA")]
    TOLA
}
