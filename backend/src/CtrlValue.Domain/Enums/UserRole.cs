using System.ComponentModel;

namespace CtrlValue.Domain.Enums;

public enum UserRole
{
    [Description("SuperAdmin")]
    SuperAdmin,
    [Description("SiteAdmin")]
    SiteAdmin,
    [Description("User")]
    User
}
