using System.ComponentModel;
namespace CtrlValue.Domain.Enums;

public enum TransactionType
{
    [Description("Income")]
    Income,
    [Description("Expense")]
    Expense,
    [Description("Asset Purchase")]
    AssetPurchase,
    [Description("Asset Sale")]
    AssetSale,
    [Description("Transfer")]
    Transfer,
    [Description("Loan Disbursement")]
    LoanDisbursement,
    [Description("Loan Repayment")]
    LoanRepayment,
    [Description("Capital Deposit")]
    CapitalDeposit,
    [Description("Capital Withdrawal")]
    CapitalWithdrawal,
    /// <summary>
    /// System-reserved anchor transaction. Represents the known starting balance
    /// as of a specific date. Excluded from inflow/outflow sums during balance recalculation.
    /// </summary>
    [Description("Opening Balance")]
    OpeningBalance,
    /// <summary>System-generated periodic interest charge on a loan account.</summary>
    [Description("Loan Interest Charge")]
    LoanInterestCharge
}

//| Type | Cashflow | NetWorth | PnL |
//| --------- | -------- | -------- | --------- |
//| BUY(ETF) | Yes | No | No |
//| SELL | Yes | No | Gain only |
//| INCOME | Yes | Yes | Yes |
//| EXPENSE | Yes | Yes | Yes |
//| TRANSFER | No | No | No |
//| LOAN_DRAW | Yes | No | No |
