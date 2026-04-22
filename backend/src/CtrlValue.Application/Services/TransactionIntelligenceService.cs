using Microsoft.EntityFrameworkCore;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain.Entities;
using CtrlValue.Domain.Enums;
using CtrlValue.Infrastructure.Data;

namespace CtrlValue.Application.Services;

/// <summary>
/// Analyses committed transactions to surface intelligence:
///   1. Transfer detection  — match outflow/inflow pairs across own accounts.
///   2. Subscription detection — cluster by merchant + amount + cadence.
///   3. Spending trends, top merchants, cash-flow summaries.
///
/// All detection is purely in-memory after a single DB fetch.
/// No AI/ML dependency — heuristics only; weights tuned for AU/NZ retail banking patterns.
/// </summary>
public class TransactionIntelligenceService : ITransactionIntelligenceService
{
    private readonly AppDbContext _db;

    public TransactionIntelligenceService(AppDbContext db)
    {
        _db = db;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Transfer Detection
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<List<TransferCandidateDto>> DetectTransferCandidatesAsync(
        Guid entityId, int lookbackDays = 7)
    {
        var since = DateTime.UtcNow.AddDays(-lookbackDays);

        // Fetch unlinked transactions (no TransferGroupId yet) within the window.
        var txns = await _db.Transactions
            .Include(t => t.Account)
            .Where(t => t.EntityId == entityId
                     && !t.IsDeleted
                     && t.TxnTime >= since
                     && t.TransferGroupId == null
                     && (t.TxnType == TransactionType.Income || t.TxnType == TransactionType.Expense))
            .OrderBy(t => t.TxnTime)
            .ToListAsync();

        var outflows = txns.Where(t => t.Direction == TransactionDirection.Outflow).ToList();
        var inflows  = txns.Where(t => t.Direction == TransactionDirection.Inflow).ToList();

        var candidates = new List<TransferCandidateDto>();
        var usedInflows  = new HashSet<Guid>();
        var usedOutflows = new HashSet<Guid>();

        // For each outflow, try to find the best-matching inflow
        foreach (var outflow in outflows)
        {
            TransferCandidateDto? best = null;

            foreach (var inflow in inflows)
            {
                if (usedInflows.Contains(inflow.Id)) continue;
                if (inflow.AccountId == outflow.AccountId) continue;  // same account → not a transfer
                if (inflow.Currency != outflow.Currency)  continue;

                // Amount must be within 1% to accommodate bank fees on conversion
                if (!AmountsMatch(outflow.Amount, inflow.Amount)) continue;

                var timeDiff = Math.Abs((inflow.TxnTime - outflow.TxnTime).TotalDays);
                if (timeDiff > lookbackDays) continue;

                var confidence = ScoreTransferConfidence(outflow, inflow, timeDiff);
                if (confidence < 0.40) continue;  // below 40% is noise

                if (best == null || confidence > best.Confidence)
                {
                    best = new TransferCandidateDto(
                        OutflowTxnId:      outflow.Id,
                        OutflowDescription: outflow.Description,
                        OutflowAccount:    outflow.Account?.Name ?? outflow.AccountId.ToString(),
                        OutflowDate:       outflow.TxnTime,

                        InflowTxnId:       inflow.Id,
                        InflowDescription: inflow.Description,
                        InflowAccount:     inflow.Account?.Name ?? inflow.AccountId.ToString(),
                        InflowDate:        inflow.TxnTime,

                        Amount:     outflow.Amount,
                        Currency:   outflow.Currency,
                        Confidence: Math.Round(confidence, 2)
                    );
                }
            }

            if (best != null)
            {
                candidates.Add(best);
                usedInflows.Add(best.InflowTxnId);
                usedOutflows.Add(best.OutflowTxnId);
            }
        }

        return candidates.OrderByDescending(c => c.Confidence).ToList();
    }

    private static bool AmountsMatch(decimal a, decimal b)
    {
        if (a == 0 && b == 0) return true;
        var max  = Math.Max(Math.Abs(a), Math.Abs(b));
        return Math.Abs(a - b) / max <= 0.01m;  // within 1%
    }

    private static double ScoreTransferConfidence(
        Transaction outflow, Transaction inflow, double timeDiffDays)
    {
        double score = 0.0;

        // Same-day → strong signal (0.40), within 1 day → (0.30), up to 3 days (0.15)
        score += timeDiffDays == 0 ? 0.40
               : timeDiffDays <= 1 ? 0.30
               : timeDiffDays <= 3 ? 0.15
               : 0.05;

        // Exact amount match → 0.30
        score += outflow.Amount == inflow.Amount ? 0.30 : 0.15;

        // Description similarity (simple token overlap)
        double sim = DescriptionSimilarity(outflow.Description, inflow.Description);
        score += sim * 0.20;

        // Keywords that suggest transfers
        var outDesc = outflow.Description.ToLowerInvariant();
        var inDesc  = inflow.Description.ToLowerInvariant();
        var transferKeywords = new[] { "transfer", "trf", "tfr", "xfer", "payment to", "payment from", "direct credit", "direct debit" };
        if (transferKeywords.Any(k => outDesc.Contains(k) || inDesc.Contains(k)))
            score += 0.10;

        return Math.Min(score, 1.0);
    }

    private static double DescriptionSimilarity(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return 0;
        var tokensA = Tokenise(a);
        var tokensB = Tokenise(b);
        if (tokensA.Count == 0 || tokensB.Count == 0) return 0;
        var intersection = tokensA.Intersect(tokensB).Count();
        return (double)intersection / Math.Max(tokensA.Count, tokensB.Count);
    }

    private static HashSet<string> Tokenise(string s)
        => new(s.ToLowerInvariant()
                 .Split([' ', '-', '_', '/', '\\', '.', ','], StringSplitOptions.RemoveEmptyEntries)
                 .Where(t => t.Length > 2));

    public async Task LinkTransferAsync(Guid outflowTxnId, Guid inflowTxnId, Guid entityId)
    {
        var txns = await _db.Transactions
            .Where(t => (t.Id == outflowTxnId || t.Id == inflowTxnId)
                     && t.EntityId == entityId
                     && !t.IsDeleted)
            .ToListAsync();

        var outflow = txns.FirstOrDefault(t => t.Id == outflowTxnId)
            ?? throw new KeyNotFoundException($"Outflow transaction {outflowTxnId} not found.");
        var inflow = txns.FirstOrDefault(t => t.Id == inflowTxnId)
            ?? throw new KeyNotFoundException($"Inflow transaction {inflowTxnId} not found.");

        var groupId = Guid.NewGuid();
        outflow.TransferGroupId = groupId;
        outflow.RelatedTxnId   = inflowTxnId;
        outflow.TxnType        = TransactionType.Expense;
        outflow.Direction      = TransactionDirection.Outflow;

        inflow.TransferGroupId = groupId;
        inflow.RelatedTxnId   = outflowTxnId;
        inflow.TxnType        = TransactionType.Income;
        inflow.Direction      = TransactionDirection.Inflow;

        await _db.SaveChangesAsync();
    }

    public async Task UnlinkTransferAsync(Guid transferGroupId, Guid entityId)
    {
        var legs = await _db.Transactions
            .Where(t => t.TransferGroupId == transferGroupId
                     && t.EntityId == entityId
                     && !t.IsDeleted)
            .ToListAsync();

        foreach (var leg in legs)
        {
            leg.TransferGroupId = null;
            leg.RelatedTxnId   = null;
        }

        await _db.SaveChangesAsync();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Subscription / Recurring Detection
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<List<RecurringPatternDto>> DetectRecurringPatternsAsync(
        Guid entityId, int lookbackMonths = 6)
    {
        var since = DateTime.UtcNow.AddMonths(-lookbackMonths);

        var txns = await _db.Transactions
            .Where(t => t.EntityId == entityId
                     && !t.IsDeleted
                     && t.TxnTime >= since
                     && t.Direction == TransactionDirection.Outflow
                     && t.TransferGroupId == null   // exclude internal transfers
                     && t.Amount > 0)
            .OrderBy(t => t.TxnTime)
            .ToListAsync();

        // Group by normalised merchant / description key + approximate amount bucket
        var groups = txns
            .GroupBy(t => BuildRecurringKey(t))
            .Where(g => g.Count() >= 2)     // at least 2 occurrences to be "recurring"
            .ToList();

        var results = new List<RecurringPatternDto>();

        foreach (var group in groups)
        {
            var sorted   = group.OrderBy(t => t.TxnTime).ToList();
            var first    = sorted.First();
            var last     = sorted.Last();
            var count    = sorted.Count;
            var avgAmount = sorted.Average(t => t.Amount);

            var cadence  = InferCadence(sorted);
            var nextDate = PredictNextDate(last.TxnTime, cadence);
            var display  = NormaliseMerchant(first.Merchant ?? first.Description);

            results.Add(new RecurringPatternDto(
                MerchantNormalised: group.Key,
                DisplayName:        display,
                TypicalAmount:      Math.Round(avgAmount, 2),
                Currency:           first.Currency,
                Cadence:            cadence,
                OccurrenceCount:    count,
                FirstSeen:          first.TxnTime,
                LastSeen:           last.TxnTime,
                PredictedNextDate:  nextDate,
                TransactionIds:     sorted.Select(t => t.Id).ToList()
            ));
        }

        return results
            .OrderByDescending(r => r.TypicalAmount)
            .ToList();
    }

    private static string BuildRecurringKey(Transaction t)
    {
        var merchant = NormaliseMerchant(t.Merchant ?? t.Description);
        // Round amount to nearest $0.50 bucket so minor price changes still cluster
        var amountBucket = Math.Round(t.Amount / 0.5m) * 0.5m;
        return $"{merchant}|{amountBucket}|{t.Currency}";
    }

    private static string NormaliseMerchant(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "unknown";

        // Strip common noise suffixes: location codes, dates, reference numbers
        var cleaned = raw.Trim().ToLowerInvariant();

        // Remove trailing numeric references (e.g. "Netflix 123456789")
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s*\d{5,}$", "");
        // Remove date-like suffixes
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s*\d{2}[/\-]\d{2}.*$", "");
        // Collapse repeated spaces
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s+", " ").Trim();

        return cleaned.Length > 0 ? cleaned : "unknown";
    }

    private static string InferCadence(List<Transaction> sorted)
    {
        if (sorted.Count < 2) return "Irregular";

        // Calculate gaps (in days) between consecutive transactions
        var gaps = sorted
            .Zip(sorted.Skip(1), (a, b) => (b.TxnTime - a.TxnTime).TotalDays)
            .ToList();

        var avgGap = gaps.Average();
        var stdDev = Math.Sqrt(gaps.Average(g => Math.Pow(g - avgGap, 2)));

        // High variance → irregular
        if (stdDev > avgGap * 0.40) return "Irregular";

        return avgGap switch
        {
            < 8              => "Weekly",
            >= 8 and < 18    => "Fortnightly",
            >= 18 and < 45   => "Monthly",
            >= 45 and < 100  => "Quarterly",
            >= 300           => "Annual",
            _                => "Irregular"
        };
    }

    private static DateTime? PredictNextDate(DateTime lastSeen, string cadence)
    {
        return cadence switch
        {
            "Weekly"      => lastSeen.AddDays(7),
            "Fortnightly" => lastSeen.AddDays(14),
            "Monthly"     => lastSeen.AddMonths(1),
            "Quarterly"   => lastSeen.AddMonths(3),
            "Annual"      => lastSeen.AddYears(1),
            _             => null
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Spending Intelligence
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<List<SpendingByMonthDto>> GetSpendingTrendAsync(
        Guid entityId, int months = 6)
    {
        var since = DateTime.UtcNow.AddMonths(-months);

        var data = await _db.Transactions
            .Include(t => t.Category)
            .Where(t => t.EntityId == entityId
                     && !t.IsDeleted
                     && t.TxnTime >= since
                     && t.Direction == TransactionDirection.Outflow
                     && t.TxnType == TransactionType.Expense
                     && t.TransferGroupId == null)
            .Select(t => new
            {
                t.TxnTime.Year,
                t.TxnTime.Month,
                CategoryName = t.Category != null ? t.Category.Name : "Uncategorised",
                t.Amount
            })
            .ToListAsync();

        return data
            .GroupBy(t => new { t.Year, t.Month, t.CategoryName })
            .Select(g => new SpendingByMonthDto(
                g.Key.Year,
                g.Key.Month,
                g.Key.CategoryName,
                Math.Round(g.Sum(t => t.Amount), 2)
            ))
            .OrderBy(r => r.Year).ThenBy(r => r.Month)
            .ToList();
    }

    public async Task<List<MerchantSpendDto>> GetTopMerchantsAsync(
        Guid entityId, DateTime from, DateTime to, int topN = 10)
    {
        var data = await _db.Transactions
            .Where(t => t.EntityId == entityId
                     && !t.IsDeleted
                     && t.TxnTime >= from
                     && t.TxnTime <= to
                     && t.Direction == TransactionDirection.Outflow
                     && t.TxnType == TransactionType.Expense
                     && t.TransferGroupId == null
                     && t.Merchant != null)
            .Select(t => new { t.Merchant, t.Amount, t.Currency })
            .ToListAsync();

        return data
            .GroupBy(t => t.Merchant!)
            .Select(g => new MerchantSpendDto(
                Merchant:         g.Key,
                TotalSpend:       Math.Round(g.Sum(t => t.Amount), 2),
                TransactionCount: g.Count(),
                Currency:         g.First().Currency
            ))
            .OrderByDescending(m => m.TotalSpend)
            .Take(topN)
            .ToList();
    }

    public async Task<List<CashFlowMonthDto>> GetCashFlowAsync(
        Guid entityId, int months = 6)
    {
        var since = DateTime.UtcNow.AddMonths(-months);

        var data = await _db.Transactions
            .Where(t => t.EntityId == entityId
                     && !t.IsDeleted
                     && t.TxnTime >= since
                     && t.TransferGroupId == null  // exclude internal transfers
                     && (t.TxnType == TransactionType.Income || t.TxnType == TransactionType.Expense))
            .Select(t => new
            {
                t.TxnTime.Year,
                t.TxnTime.Month,
                t.Direction,
                t.Amount
            })
            .ToListAsync();

        var byMonth = data
            .GroupBy(t => new { t.Year, t.Month })
            .Select(g => new CashFlowMonthDto(
                Year:           g.Key.Year,
                Month:          g.Key.Month,
                TotalIncome:    Math.Round(g.Where(t => t.Direction == TransactionDirection.Inflow).Sum(t => t.Amount), 2),
                TotalExpenses:  Math.Round(g.Where(t => t.Direction == TransactionDirection.Outflow).Sum(t => t.Amount), 2),
                Net:            Math.Round(
                                    g.Where(t => t.Direction == TransactionDirection.Inflow).Sum(t => t.Amount) -
                                    g.Where(t => t.Direction == TransactionDirection.Outflow).Sum(t => t.Amount),
                                    2)
            ))
            .OrderBy(r => r.Year).ThenBy(r => r.Month)
            .ToList();

        return byMonth;
    }
}
