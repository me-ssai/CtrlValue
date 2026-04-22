using System.ComponentModel;

namespace CtrlValue.Domain.Enums;

public enum EntityRole
{
    [Description("OWNER")]
    OWNER,
    [Description("VIEWER")]
    VIEWER,
    [Description("EDITOR")]
    EDITOR
}
