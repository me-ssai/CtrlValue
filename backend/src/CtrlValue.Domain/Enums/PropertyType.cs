using System.ComponentModel;

namespace CtrlValue.Domain.Enums;

public enum PropertyType
{
    [Description("RESIDENTIAL")]
    RESIDENTIAL,
    [Description("COMMERCIAL")]
    COMMERCIAL,
    [Description("INDUSTRIAL")]
    INDUSTRIAL,
    [Description("LAND")]
    LAND,
    [Description("RURAL")]
    RURAL
}
