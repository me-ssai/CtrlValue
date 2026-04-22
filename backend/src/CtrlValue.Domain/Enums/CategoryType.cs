using System.ComponentModel;

namespace CtrlValue.Domain.Enums;

public enum CategoryType
{
    [Description("INCOME")]
    INCOME, 
    [Description("EXPENSE")]
    EXPENSE, 
    [Description("TRANSFER")]
    TRANSFER
}
