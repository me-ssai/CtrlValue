using System.ComponentModel;

namespace CtrlValue.Domain.Enums;

public enum DepreciationMethod
{
    [Description("STRAIGHT_LINE")]
    STRAIGHT_LINE,
    [Description("DECLINING_BALANCE")]
    DECLINING_BALANCE,
    [Description("REDBOOK")]
    REDBOOK
}
