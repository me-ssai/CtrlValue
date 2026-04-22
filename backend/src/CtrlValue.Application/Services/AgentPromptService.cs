using CtrlValue.Application.DTOs;

namespace CtrlValue.Application.Services;

/// <summary>
/// Assembles the system prompt and conversation history for LLM calls.
/// The prompt version is embedded in every prompt and recorded in the audit log.
/// </summary>
public static class AgentPromptService
{
    public const string PromptTemplateVersion = "1.0.0";

    /// <summary>
    /// Builds the system prompt that frames every chat conversation.
    /// Injects the user's financial snapshot so the model has full context.
    /// </summary>
    public static string BuildChatSystemPrompt(FinanceContextDto ctx)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("You are CtrlValue, a financial intelligence assistant integrated into Project Z.");
        sb.AppendLine("Your role is to provide educational financial commentary and help the user understand their financial position.");
        sb.AppendLine("You do NOT provide regulated financial advice. Never instruct the user to buy, sell, or invest in specific assets.");
        sb.AppendLine();
        sb.AppendLine("IMPORTANT RULES:");
        sb.AppendLine("- Use language like \"generally\", \"may\", \"could\", \"worth reviewing\", \"based on your data\"");
        sb.AppendLine("- Always distinguish: data from the user's accounts (Internal) vs web research (Web) vs inference");
        sb.AppendLine("- If uncertain, say so explicitly");
        sb.AppendLine("- Never fabricate figures not present in the snapshot below");
        sb.AppendLine("- Do not recommend specific investment products, funds, or timing");
        sb.AppendLine();
        sb.AppendLine($"USER'S FINANCIAL SNAPSHOT (as of {ctx.AsOf:yyyy-MM-dd}, currency: {ctx.Currency}):");
        sb.AppendLine();
        sb.AppendLine("NET WORTH:");
        sb.AppendLine($"  Total:       {ctx.Currency} {ctx.NetWorth.Total:N0}");
        sb.AppendLine($"  Assets:      {ctx.Currency} {ctx.NetWorth.TotalAssets:N0}");
        sb.AppendLine($"  Liabilities: {ctx.Currency} {ctx.NetWorth.TotalLiabilities:N0}");
        if (ctx.NetWorth.ByAssetClass.Count > 0)
        {
            sb.AppendLine("  By asset class:");
            foreach (var (cls, val) in ctx.NetWorth.ByAssetClass.OrderByDescending(kv => kv.Value))
                sb.AppendLine($"    {cls}: {ctx.Currency} {val:N0}");
        }

        sb.AppendLine();
        sb.AppendLine("CASH POSITION:");
        sb.AppendLine($"  Total cash:          {ctx.Currency} {ctx.Cash.TotalCashBalance:N0}");
        sb.AppendLine($"  Idle cash estimate:  {ctx.Currency} {ctx.Cash.EstimatedIdleCash:N0}");
        sb.AppendLine($"  Emergency buffer:    {ctx.Currency} {ctx.Cash.EmergencyFundEstimate:N0}");
        sb.AppendLine($"  Monthly surplus:     {ctx.Currency} {ctx.Cash.MonthlyCashSurplusDeficit:N0}");

        sb.AppendLine();
        sb.AppendLine("INCOME & SAVING (last 6 months avg):");
        sb.AppendLine($"  Monthly income:    {ctx.Currency} {ctx.Saving.AverageMonthlyIncome:N0}");
        sb.AppendLine($"  Monthly expenses:  {ctx.Currency} {ctx.Saving.AverageMonthlyExpenses:N0}");
        sb.AppendLine($"  Monthly savings:   {ctx.Currency} {ctx.Saving.AverageMonthlySavings:N0}");
        sb.AppendLine($"  Savings rate:      {ctx.Saving.SavingsRatePercent:N1}%");
        sb.AppendLine($"  Consistency:       {ctx.Saving.Consistency}");

        sb.AppendLine();
        sb.AppendLine("SPENDING (last 3 months avg):");
        sb.AppendLine($"  Total monthly avg: {ctx.Currency} {ctx.Spending.MonthlyAverageTotal:N0}");
        sb.AppendLine($"  Essential est:     {ctx.Currency} {ctx.Spending.EssentialEstimate:N0}");
        sb.AppendLine($"  Discretionary est: {ctx.Currency} {ctx.Spending.DiscretionaryEstimate:N0}");
        sb.AppendLine($"  Spend trend:       {ctx.Spending.TrendDirection}");
        if (ctx.Spending.TopCategories.Count > 0)
        {
            sb.AppendLine("  Top categories:");
            foreach (var cat in ctx.Spending.TopCategories.Take(5))
                sb.AppendLine($"    {cat.CategoryName}: {ctx.Currency} {cat.MonthlyAverage:N0}/month ({cat.PercentOfTotal:N0}%)");
        }
        if (ctx.Spending.MonthlySubscriptionTotal > 0)
            sb.AppendLine($"  Subscriptions:     {ctx.Currency} {ctx.Spending.MonthlySubscriptionTotal:N0}/month ({ctx.Spending.Subscriptions.Count} detected)");

        sb.AppendLine();
        sb.AppendLine("LIABILITIES:");
        sb.AppendLine($"  Total debt:              {ctx.Currency} {ctx.Liabilities.TotalDebt:N0}");
        sb.AppendLine($"  Monthly repayments:      {ctx.Currency} {ctx.Liabilities.TotalMonthlyRepayments:N0}");
        sb.AppendLine($"  Debt-to-income ratio:    {ctx.Liabilities.DebtToIncomeRatio * 100:N0}%");
        if (ctx.Liabilities.Liabilities.Count > 0)
        {
            sb.AppendLine("  Liabilities:");
            foreach (var l in ctx.Liabilities.Liabilities)
            {
                var rateStr = l.InterestRate.HasValue ? $", {l.InterestRate:N2}% p.a." : "";
                var repStr = l.MonthlyRepayment.HasValue
                    ? $", {ctx.Currency} {l.MonthlyRepayment:N0}/month repayment" : "";
                sb.AppendLine($"    {l.Name}: {ctx.Currency} {l.Balance:N0}{rateStr}{repStr}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("INVESTMENTS:");
        sb.AppendLine($"  Total value: {ctx.Currency} {ctx.Investments.TotalValue:N0}");
        if (ctx.Investments.ByAssetClass.Count > 0)
        {
            sb.AppendLine("  By asset class:");
            foreach (var (cls, val) in ctx.Investments.ByAssetClass.OrderByDescending(kv => kv.Value))
                sb.AppendLine($"    {cls}: {ctx.Currency} {val:N0}");
        }
        if (ctx.Investments.HasConcentrationRisk)
            sb.AppendLine($"  Concentration risk: {ctx.Investments.LargestConcentration}");

        sb.AppendLine();
        sb.AppendLine("ASSETS:");
        sb.AppendLine($"  Income-producing value:  {ctx.Currency} {ctx.Assets.IncomeProducingAssetValue:N0}");
        sb.AppendLine($"  Cost-generating value:   {ctx.Currency} {ctx.Assets.CostGeneratingAssetValue:N0}");
        if (ctx.Assets.VehicleCount > 0)
            sb.AppendLine($"  Vehicles: {ctx.Assets.VehicleCount} ({string.Join(", ", ctx.Assets.CostGeneratingAssetNames)})");

        sb.AppendLine();
        sb.AppendLine($"Prompt version: {PromptTemplateVersion}");
        sb.AppendLine("Respond conversationally. Be specific to the user's numbers when relevant.");

        return sb.ToString();
    }

    /// <summary>
    /// Builds the prompt for a macro web research query.
    /// </summary>
    public static string BuildMacroResearchPrompt(string query)
    {
        return $"Search for current information about: {query}\n\n" +
               "Summarise the current economic situation for this topic in 2-4 sentences. " +
               "Be factual and educational. Include the approximate dates of the information found. " +
               "Do not give personal financial advice. Do not recommend any specific investments.";
    }
}
