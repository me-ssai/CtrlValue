using CtrlValue.Domain.Enums;

namespace CtrlValue.Domain.Utilities;

/// <summary>
/// Converts metal quantities between different units of measurement.
/// Conversion factors are based on troy weight standards used in precious metals trading.
/// </summary>
public static class MetalUnitConverter
{
    // All factors are relative to 1 troy ounce (the industry standard quote unit)
    private static readonly Dictionary<MetalUnit, decimal> ToTroyOz = new()
    {
        [MetalUnit.TROY_OZ]  = 1m,
        [MetalUnit.GRAM]     = 0.0321507m,   // 1g = 1/31.1035 troy oz
        [MetalUnit.KILOGRAM] = 32.1507m,     // 1kg = 1000g / 31.1035
        [MetalUnit.TOLA]     = 0.374976m,    // 1 tola = 11.6638g / 31.1035
        [MetalUnit.UNIT]     = 1m            // non-metal fallback — no conversion
    };

    /// <summary>
    /// Converts <paramref name="quantity"/> from <paramref name="from"/> to <paramref name="to"/>.
    /// </summary>
    public static decimal Convert(decimal quantity, MetalUnit from, MetalUnit to)
    {
        if (from == to) return quantity;
        var inTroyOz = quantity * ToTroyOz[from];
        return inTroyOz / ToTroyOz[to];
    }

    /// <summary>
    /// Returns the display label for a unit (e.g. "troy oz", "g", "kg").
    /// </summary>
    public static string Label(MetalUnit unit) => unit switch
    {
        MetalUnit.TROY_OZ  => "troy oz",
        MetalUnit.GRAM     => "g",
        MetalUnit.KILOGRAM => "kg",
        MetalUnit.TOLA     => "tola",
        _                  => "units"
    };
}
