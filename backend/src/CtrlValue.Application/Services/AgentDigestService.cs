using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain.Entities;
using CtrlValue.Infrastructure.Data;

namespace CtrlValue.Application.Services;

public class AgentDigestService : IAgentDigestService
{
    private readonly AppDbContext _db;
    private readonly IAgentContextBuilderService _contextBuilder;
    private readonly ILogger<AgentDigestService> _logger;

    public AgentDigestService(
        AppDbContext db,
        IAgentContextBuilderService contextBuilder,
        ILogger<AgentDigestService> logger)
    {
        _db = db;
        _contextBuilder = contextBuilder;
        _logger = logger;
    }

    public async Task<AgentDigestEmailDto> GenerateDigestAsync(
        Guid userId,
        Guid entityId,
        CancellationToken ct = default)
    {
        var weekKey = GetIsoWeekKey(DateTime.UtcNow);

        // Avoid duplicate generation for same week
        var existing = await _db.AgentDigestEmails
            .FirstOrDefaultAsync(d => d.EntityId == entityId && d.WeekKey == weekKey, ct);

        if (existing != null)
            return MapToDto(existing);

        var ctx = await _contextBuilder.BuildContextAsync(userId, entityId, forceRefresh: false, ct);

        var subject = $"Your Weekly Financial Digest — {DateTime.UtcNow:MMMM d, yyyy}";
        var html = BuildDigestHtml(ctx, weekKey);

        var digest = new AgentDigestEmail
        {
            UserId   = userId,
            EntityId = entityId,
            Subject  = subject,
            HtmlBody = html,
            Status   = "Pending",
            WeekKey  = weekKey,
            TenantId = ""
        };

        _db.AgentDigestEmails.Add(digest);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("[AgentDigest] Generated digest {Id} for entity {EntityId} week {WeekKey}",
            digest.Id, entityId, weekKey);

        return MapToDto(digest);
    }

    public async Task<List<AgentDigestEmailDto>> GetPendingDigestsAsync()
    {
        var rows = await _db.AgentDigestEmails
            .AsNoTracking()
            .Where(d => d.Status == "Pending")
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        return rows.Select(MapToDto).ToList();
    }

    public async Task<List<AgentDigestEmailDto>> GetAllDigestsAsync(int page = 1, int pageSize = 50)
    {
        var rows = await _db.AgentDigestEmails
            .AsNoTracking()
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return rows.Select(MapToDto).ToList();
    }

    public async Task ApproveDigestAsync(Guid digestId, Guid approverUserId)
    {
        var digest = await _db.AgentDigestEmails.FindAsync(digestId)
            ?? throw new KeyNotFoundException("Digest not found.");

        digest.Status = "Approved";
        digest.ApprovedAt = DateTime.UtcNow;
        digest.ApprovedByUserId = approverUserId;

        await _db.SaveChangesAsync();
    }

    public async Task RejectDigestAsync(Guid digestId, Guid approverUserId)
    {
        var digest = await _db.AgentDigestEmails.FindAsync(digestId)
            ?? throw new KeyNotFoundException("Digest not found.");

        digest.Status = "Rejected";
        digest.ApprovedAt = DateTime.UtcNow;
        digest.ApprovedByUserId = approverUserId;

        await _db.SaveChangesAsync();
    }

    public async Task MarkSentAsync(Guid digestId)
    {
        var digest = await _db.AgentDigestEmails.FindAsync(digestId)
            ?? throw new KeyNotFoundException("Digest not found.");

        digest.Status = "Sent";
        digest.SentAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────

    private static string GetIsoWeekKey(DateTime date)
    {
        var cal = System.Globalization.CultureInfo.InvariantCulture.Calendar;
        int week = cal.GetWeekOfYear(date,
            System.Globalization.CalendarWeekRule.FirstFourDayWeek,
            DayOfWeek.Monday);
        return $"{date.Year}-W{week:D2}";
    }

    private static string BuildDigestHtml(FinanceContextDto ctx, string weekKey)
    {
        var currency = ctx.Currency;
        var savingsColor = ctx.Saving.SavingsRatePercent >= 10 ? "#16a34a" : "#dc2626";
        var spendTrend = ctx.Spending.TrendDirection;
        var trendIcon = spendTrend == "Up" ? "⬆" : spendTrend == "Down" ? "⬇" : "→";

        var topCatsHtml = string.Join("", ctx.Spending.TopCategories.Take(5).Select(c =>
            $"<tr><td style='padding:4px 8px;'>{c.CategoryName}</td>" +
            $"<td style='padding:4px 8px; text-align:right; font-weight:600;'>{currency} {c.MonthlyAverage:N0}/mo</td></tr>"));

        var insightHints = new List<string>();
        if (ctx.Saving.SavingsRatePercent < 10)
            insightHints.Add("Your savings rate is below 10% — consider reviewing discretionary spending.");
        if (ctx.Cash.EstimatedIdleCash > 10_000)
            insightHints.Add($"You have {currency} {ctx.Cash.EstimatedIdleCash:N0} in estimated idle cash above your emergency buffer.");
        if (ctx.Investments.HasConcentrationRisk)
            insightHints.Add($"Investment concentration risk detected: {ctx.Investments.LargestConcentration}.");

        var insightsHtml = insightHints.Count > 0
            ? "<ul>" + string.Join("", insightHints.Select(h => $"<li>{h}</li>")) + "</ul>"
            : "<p>No major flags this week. Keep it up!</p>";

        return $"""
            <!DOCTYPE html>
            <html>
            <head><meta charset='utf-8' /></head>
            <body style='font-family: Inter, Arial, sans-serif; max-width: 600px; margin: auto; padding: 24px; color: #1f2937;'>
              <h1 style='font-size: 22px; margin-bottom: 4px;'>Weekly Financial Digest</h1>
              <p style='color: #6b7280; font-size: 13px; margin-top: 0;'>Week {weekKey} &mdash; Generated by CtrlValue Agent</p>

              <table style='width:100%; border-collapse:collapse; margin: 16px 0;'>
                <tr>
                  <td style='padding:12px; background:#f0fdf4; border-radius:8px; text-align:center;'>
                    <div style='font-size:13px; color:#374151;'>Net Worth</div>
                    <div style='font-size:22px; font-weight:700;'>{currency} {ctx.NetWorth.Total:N0}</div>
                  </td>
                  <td style='width:12px;'></td>
                  <td style='padding:12px; background:#eff6ff; border-radius:8px; text-align:center;'>
                    <div style='font-size:13px; color:#374151;'>Savings Rate</div>
                    <div style='font-size:22px; font-weight:700; color:{savingsColor};'>{ctx.Saving.SavingsRatePercent:N1}%</div>
                  </td>
                  <td style='width:12px;'></td>
                  <td style='padding:12px; background:#fdf4ff; border-radius:8px; text-align:center;'>
                    <div style='font-size:13px; color:#374151;'>Spending Trend</div>
                    <div style='font-size:22px; font-weight:700;'>{trendIcon} {spendTrend}</div>
                  </td>
                </tr>
              </table>

              <h2 style='font-size:16px; margin-top:24px;'>Top Spending Categories</h2>
              <table style='width:100%;'>
                {topCatsHtml}
              </table>

              <h2 style='font-size:16px; margin-top:24px;'>Key Flags</h2>
              {insightsHtml}

              <hr style='border:none; border-top:1px solid #e5e7eb; margin:24px 0;' />
              <p style='font-size:11px; color:#9ca3af;'>
                This digest is generated from your financial data in Project Z and is for educational purposes only.
                It does not constitute financial advice. CtrlValue Agent &mdash; {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC
              </p>
            </body>
            </html>
            """;
    }

    private static AgentDigestEmailDto MapToDto(AgentDigestEmail d) => new()
    {
        Id          = d.Id,
        UserId      = d.UserId,
        EntityId    = d.EntityId,
        Subject     = d.Subject,
        HtmlBody    = d.HtmlBody,
        Status      = d.Status,
        WeekKey     = d.WeekKey,
        ApprovedAt  = d.ApprovedAt,
        SentAt      = d.SentAt,
        CreatedAt   = d.CreatedAt
    };
}
