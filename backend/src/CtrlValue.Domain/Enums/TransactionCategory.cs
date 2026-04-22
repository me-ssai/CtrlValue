using System.ComponentModel;

namespace CtrlValue.Domain.Enums;

/// <summary>
/// Domain-based grouping for TransactionType — used for UI dropdowns,
/// reporting filters, and validation rules.
/// </summary>
public enum TransactionCategory
{
    [Description("Operations")]
    Operations,     // Day-to-day income & expenses

    [Description("Investments")]
    Investments,    // Buying & selling assets / instruments

    [Description("Transfers")]
    Transfers,      // Moving money between accounts (no net worth change)

    [Description("Debt")]
    Debt,           // Loan drawdowns & repayments

    [Description("Equity")]
    Equity          // Owner capital injections & withdrawals
}

/// <summary>
/// Decorates a <see cref="TransactionType"/> value with its domain category.
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class TransactionCategoryAttribute(TransactionCategory category) : Attribute
{
    public TransactionCategory Category { get; } = category;
}

public static class TransactionTypeExtensions
{
    /// <summary>Returns the domain category for a given transaction type.</summary>
    public static TransactionCategory GetCategory(this TransactionType type)
    {
        var field = typeof(TransactionType).GetField(type.ToString());
        var attr  = field?.GetCustomAttributes(typeof(TransactionCategoryAttribute), false)
                         .Cast<TransactionCategoryAttribute>()
                         .FirstOrDefault();
        return attr?.Category ?? TransactionCategory.Operations;
    }

    /// <summary>
    /// Returns all transaction types grouped by their domain category,
    /// e.g. for building a grouped dropdown on the frontend.
    /// </summary>
    public static IEnumerable<TransactionTypeGroup> GetGrouped()
        => Enum.GetValues<TransactionType>()
               .GroupBy(t => t.GetCategory())
               .Select(g => new TransactionTypeGroup(
                   g.Key.ToString(),
                   g.Select(t => new TransactionTypeOption(t.ToString(), t.GetDescription()))
               ));
}

public record TransactionTypeGroup(string Category, IEnumerable<TransactionTypeOption> Types);
public record TransactionTypeOption(string Value, string Label);

public static class EnumExtensions
{
    public static string GetDescription(this Enum value)
    {
        var field = value.GetType().GetField(value.ToString());
        var attr  = field?.GetCustomAttributes(typeof(DescriptionAttribute), false)
                         .Cast<DescriptionAttribute>()
                         .FirstOrDefault();
        return attr?.Description ?? value.ToString();
    }
}
