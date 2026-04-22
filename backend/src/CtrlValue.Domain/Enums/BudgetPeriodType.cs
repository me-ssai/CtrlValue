using System.ComponentModel;

namespace CtrlValue.Domain.Enums;

public enum BudgetPeriodType
{
    [Description("MONTHLY")]
    MONTHLY,
    [Description("QUARTERLY")]
    QUARTERLY,
    [Description("ANNUAL")]
    ANNUAL
}
