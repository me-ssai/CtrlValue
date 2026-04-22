using Microsoft.EntityFrameworkCore;
using CtrlValue.Application.DTOs;
using CtrlValue.Domain.Entities;
using CtrlValue.Domain.Enums;
using CtrlValue.Infrastructure.Data;

namespace CtrlValue.Application.Services;

public interface ILoanService
{
    Task<LoanDetailsDto> CreateLoanDetailsAsync(CreateLoanDetailsRequest request, Guid entityId);
    Task<LoanDetailsDto> UpdateLoanDetailsAsync(Guid loanId, UpdateLoanDetailsRequest request, Guid entityId);
    Task<LoanDetailsDto?> GetLoanDetailsByAccountAsync(Guid accountId, Guid entityId);
    Task<List<LoanDetailsDto>> GetAllLoansByEntityAsync(Guid entityId);
    Task<AmortisationScheduleDto> GetAmortisationScheduleAsync(Guid accountId, Guid entityId, decimal extraPaymentPerPeriod = 0);
    Task<LoanSummaryDto> GetLoanSummaryAsync(Guid accountId, Guid entityId);
    Task<LoanDetailsDto> AddRateChangeAsync(Guid loanId, LoanRateChangeRequest request, Guid entityId);
    Task RecalculateRedrawAsync(Guid accountId, Guid entityId);
}

public class LoanService : ILoanService
{
    private readonly AppDbContext _db;

    public LoanService(AppDbContext db)
    {
        _db = db;
    }

    // ── Create ────────────────────────────────────────────────────────────────

    public async Task<LoanDetailsDto> CreateLoanDetailsAsync(CreateLoanDetailsRequest request, Guid entityId)
    {
        var account = await _db.Accounts
            .FirstOrDefaultAsync(a => a.Id == request.AccountId && a.EntityId == entityId)
            ?? throw new KeyNotFoundException("Account not found.");

        if (account.AccountType != AccountType.LIABILITY)
            throw new InvalidOperationException("Loan details can only be attached to LIABILITY accounts.");

        var existing = await _db.LoanDetails.FirstOrDefaultAsync(l => l.AccountId == request.AccountId && !l.IsDeleted);
        if (existing != null)
            throw new InvalidOperationException("This account already has loan details. Use PUT to update.");

        var loan = new LoanDetails
        {
            AccountId = request.AccountId,
            EntityId = entityId,
            PropertyAccountId = request.PropertyAccountId,
            OffsetAccountId = request.OffsetAccountId,
            LoanAmount = request.LoanAmount,
            InterestRate = request.InterestRate,
            RateType = Enum.Parse<LoanRateType>(request.RateType, true),
            FixedRateExpiresAt = request.FixedRateExpiresAt?.ToUniversalTime(),
            PaymentFrequency = Enum.Parse<PaymentFrequency>(request.PaymentFrequency, true),
            RepaymentAmount = request.RepaymentAmount,
            LoanTermMonths = request.LoanTermMonths,
            StartDate = request.StartDate.ToUniversalTime(),
            NextPaymentDate = CalculateFirstPaymentDate(request.StartDate.ToUniversalTime(), Enum.Parse<PaymentFrequency>(request.PaymentFrequency, true)),
            IsInterestOnly = request.IsInterestOnly,
            Notes = request.Notes
        };

        // Seed initial rate history entry
        loan.RateHistory.Add(new LoanRateHistory
        {
            Rate = request.InterestRate,
            EffectiveFrom = request.StartDate.ToUniversalTime(),
            Notes = "Initial rate"
        });

        _db.LoanDetails.Add(loan);
        await _db.SaveChangesAsync();

        return await BuildDetailsDtoAsync(loan, entityId);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    public async Task<LoanDetailsDto> UpdateLoanDetailsAsync(Guid loanId, UpdateLoanDetailsRequest request, Guid entityId)
    {
        var loan = await _db.LoanDetails
            .Include(l => l.RateHistory)
            .FirstOrDefaultAsync(l => l.Id == loanId && l.EntityId == entityId && !l.IsDeleted)
            ?? throw new KeyNotFoundException("Loan details not found.");

        loan.PropertyAccountId = request.PropertyAccountId;
        loan.OffsetAccountId = request.OffsetAccountId;
        loan.LoanAmount = request.LoanAmount;
        loan.InterestRate = request.InterestRate;
        loan.RateType = Enum.Parse<LoanRateType>(request.RateType, true);
        loan.FixedRateExpiresAt = request.FixedRateExpiresAt?.ToUniversalTime();
        loan.PaymentFrequency = Enum.Parse<PaymentFrequency>(request.PaymentFrequency, true);
        loan.RepaymentAmount = request.RepaymentAmount;
        loan.LoanTermMonths = request.LoanTermMonths;
        loan.StartDate = request.StartDate.ToUniversalTime();
        loan.NextPaymentDate = request.NextPaymentDate.ToUniversalTime();
        loan.IsInterestOnly = request.IsInterestOnly;
        loan.Notes = request.Notes;

        await _db.SaveChangesAsync();
        return await BuildDetailsDtoAsync(loan, entityId);
    }

    // ── Reads ─────────────────────────────────────────────────────────────────

    public async Task<LoanDetailsDto?> GetLoanDetailsByAccountAsync(Guid accountId, Guid entityId)
    {
        var loan = await _db.LoanDetails
            .Include(l => l.RateHistory)
            .FirstOrDefaultAsync(l => l.AccountId == accountId && l.EntityId == entityId && !l.IsDeleted);
        return loan == null ? null : await BuildDetailsDtoAsync(loan, entityId);
    }

    public async Task<List<LoanDetailsDto>> GetAllLoansByEntityAsync(Guid entityId)
    {
        var loans = await _db.LoanDetails
            .Include(l => l.RateHistory)
            .Where(l => l.EntityId == entityId && !l.IsDeleted)
            .ToListAsync();

        var result = new List<LoanDetailsDto>();
        foreach (var loan in loans)
            result.Add(await BuildDetailsDtoAsync(loan, entityId));
        return result;
    }

    // ── Amortisation schedule ─────────────────────────────────────────────────

    public async Task<AmortisationScheduleDto> GetAmortisationScheduleAsync(Guid accountId, Guid entityId, decimal extraPaymentPerPeriod = 0)
    {
        var loan = await _db.LoanDetails
            .Include(l => l.RateHistory)
            .FirstOrDefaultAsync(l => l.AccountId == accountId && l.EntityId == entityId && !l.IsDeleted)
            ?? throw new KeyNotFoundException("Loan details not found for this account.");

        // Get current offset balance (if linked)
        decimal offsetBalance = 0;
        if (loan.OffsetAccountId.HasValue)
        {
            var offsetAcc = await _db.Accounts
                .FirstOrDefaultAsync(a => a.Id == loan.OffsetAccountId.Value && !a.IsDeleted);
            offsetBalance = offsetAcc?.CurrentBalance ?? 0;
        }

        // Build sorted rate history: most recent first, fallback to current rate
        var rateHistory = loan.RateHistory
            .Where(r => !r.IsDeleted)
            .OrderBy(r => r.EffectiveFrom)
            .ToList();

        int periodsPerYear = loan.PaymentFrequency switch
        {
            PaymentFrequency.Weekly => 52,
            PaymentFrequency.Fortnightly => 26,
            _ => 12
        };

        var standard = BuildSchedule(loan, rateHistory, offsetBalance, 0, periodsPerYear);
        var accelerated = extraPaymentPerPeriod > 0
            ? BuildSchedule(loan, rateHistory, offsetBalance, extraPaymentPerPeriod, periodsPerYear)
            : standard;

        decimal totalInterestStandard = standard.Sum(r => r.Interest);
        decimal totalInterestAccelerated = accelerated.Sum(r => r.Interest);

        return new AmortisationScheduleDto
        {
            Standard = standard,
            Accelerated = accelerated,
            ExtraPaymentPerPeriod = extraPaymentPerPeriod,
            MonthsSaved = standard.Count - accelerated.Count,
            InterestSaved = totalInterestStandard - totalInterestAccelerated
        };
    }

    private static List<AmortisationRowDto> BuildSchedule(
        LoanDetails loan,
        List<LoanRateHistory> rateHistory,
        decimal offsetBalance,
        decimal extraPaymentPerPeriod,
        int periodsPerYear)
    {
        var rows = new List<AmortisationRowDto>();
        decimal balance = loan.LoanAmount;
        decimal repayment = loan.RepaymentAmount + extraPaymentPerPeriod;
        DateTime paymentDate = loan.NextPaymentDate;
        decimal cumulativeInterest = 0;
        int paymentNumber = 1;
        const int maxPayments = 1200; // safety cap

        while (balance > 0.01m && paymentNumber <= maxPayments)
        {
            // Pick the applicable rate for this period
            decimal annualRate = GetRateForDate(rateHistory, paymentDate, loan.InterestRate);
            decimal periodicRate = annualRate / periodsPerYear;

            // Apply offset account
            decimal effectiveBalance = Math.Max(0, balance - offsetBalance);
            decimal interest = Math.Round(effectiveBalance * periodicRate, 2);
            decimal principal;

            if (loan.IsInterestOnly)
            {
                principal = 0;
                repayment = interest;
            }
            else
            {
                principal = Math.Min(repayment - interest, balance);
                if (principal < 0) principal = 0; // safety: repayment < interest (edge case)
            }

            cumulativeInterest += interest;
            balance = Math.Round(balance - principal, 2);
            if (balance < 0) balance = 0;

            rows.Add(new AmortisationRowDto
            {
                PaymentNumber = paymentNumber,
                PaymentDate = paymentDate,
                PaymentAmount = Math.Min(repayment, balance + interest + principal),
                Principal = principal,
                Interest = interest,
                CumulativeInterest = cumulativeInterest,
                Balance = balance
            });

            // Advance to next payment date
            paymentDate = AdvanceDate(paymentDate, loan.PaymentFrequency);
            paymentNumber++;
        }

        return rows;
    }

    // ── Loan Summary ──────────────────────────────────────────────────────────

    public async Task<LoanSummaryDto> GetLoanSummaryAsync(Guid accountId, Guid entityId)
    {
        var loan = await _db.LoanDetails
            .Include(l => l.RateHistory)
            .FirstOrDefaultAsync(l => l.AccountId == accountId && l.EntityId == entityId && !l.IsDeleted)
            ?? throw new KeyNotFoundException("Loan details not found.");

        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId && !a.IsDeleted);
        decimal remainingBalance = Math.Abs(account?.CurrentBalance ?? loan.LoanAmount);

        // Offset
        decimal offsetBalance = 0;
        string? offsetAccountName = null;
        if (loan.OffsetAccountId.HasValue)
        {
            var offsetAcc = await _db.Accounts.FirstOrDefaultAsync(a => a.Id == loan.OffsetAccountId.Value && !a.IsDeleted);
            offsetBalance = offsetAcc?.CurrentBalance ?? 0;
            offsetAccountName = offsetAcc?.Name;
        }

        // Property / LVR
        decimal? propertyValue = null;
        if (loan.PropertyAccountId.HasValue)
        {
            var latestVal = await _db.Valuations
                .Where(v => v.AccountId == loan.PropertyAccountId.Value && !v.IsDeleted)
                .OrderByDescending(v => v.AsOfDate)
                .FirstOrDefaultAsync();
            propertyValue = latestVal?.Value;
        }
        decimal? lvr = (propertyValue.HasValue && propertyValue.Value > 0)
            ? Math.Round(remainingBalance / propertyValue.Value, 4)
            : null;

        // Schedule projection for months remaining & total interest
        var rateHistory = loan.RateHistory.Where(r => !r.IsDeleted).OrderBy(r => r.EffectiveFrom).ToList();
        int periodsPerYear = loan.PaymentFrequency switch
        {
            PaymentFrequency.Weekly => 52,
            PaymentFrequency.Fortnightly => 26,
            _ => 12
        };
        var schedule = BuildSchedule(loan, rateHistory, offsetBalance, 0, periodsPerYear);
        int monthsRemaining = loan.PaymentFrequency == PaymentFrequency.Monthly
            ? schedule.Count
            : (int)Math.Ceiling(schedule.Count * 12.0 / periodsPerYear);

        decimal currentRate = GetRateForDate(rateHistory, DateTime.UtcNow, loan.InterestRate);
        int? daysUntilFixedExpiry = loan.FixedRateExpiresAt.HasValue
            ? (int)(loan.FixedRateExpiresAt.Value - DateTime.UtcNow).TotalDays
            : null;

        return new LoanSummaryDto
        {
            AccountId = accountId,
            AccountName = account?.Name ?? string.Empty,
            RemainingBalance = remainingBalance,
            CurrentInterestRate = currentRate,
            RateType = loan.RateType.ToString(),
            FixedRateExpiresAt = loan.FixedRateExpiresAt,
            DaysUntilFixedRateExpiry = daysUntilFixedExpiry,
            NextPaymentAmount = loan.RepaymentAmount,
            NextPaymentDate = loan.NextPaymentDate,
            DaysUntilNextPayment = Math.Max(0, (int)(loan.NextPaymentDate - DateTime.UtcNow).TotalDays),
            RedrawAvailable = loan.RedrawAvailable,
            LVR = lvr,
            PropertyValue = propertyValue,
            MonthsRemaining = monthsRemaining,
            TotalInterestPayable = schedule.Sum(r => r.Interest),
            OffsetAccountId = loan.OffsetAccountId,
            OffsetAccountName = offsetAccountName,
            OffsetBalance = offsetBalance
        };
    }

    // ── Rate change ───────────────────────────────────────────────────────────

    public async Task<LoanDetailsDto> AddRateChangeAsync(Guid loanId, LoanRateChangeRequest request, Guid entityId)
    {
        var loan = await _db.LoanDetails
            .Include(l => l.RateHistory)
            .FirstOrDefaultAsync(l => l.Id == loanId && l.EntityId == entityId && !l.IsDeleted)
            ?? throw new KeyNotFoundException("Loan not found.");

        loan.RateHistory.Add(new LoanRateHistory
        {
            LoanDetailsId = loanId,
            Rate = request.Rate,
            EffectiveFrom = request.EffectiveFrom.ToUniversalTime(),
            Notes = request.Notes
        });

        // Update the current rate on the parent record
        loan.InterestRate = request.Rate;
        await _db.SaveChangesAsync();
        return await BuildDetailsDtoAsync(loan, entityId);
    }

    // ── Redraw recalculation ──────────────────────────────────────────────────

    public async Task RecalculateRedrawAsync(Guid accountId, Guid entityId)
    {
        var loan = await _db.LoanDetails
            .FirstOrDefaultAsync(l => l.AccountId == accountId && l.EntityId == entityId && !l.IsDeleted);
        if (loan == null) return;

        var extraRepayments = await _db.Transactions
            .Where(t => t.AccountId == accountId
                     && t.EntityId == entityId
                     && !t.IsDeleted
                     && t.IsExtraRepayment)
            .SumAsync(t => (decimal?)t.Amount) ?? 0m;

        loan.RedrawAvailable = extraRepayments;
        await _db.SaveChangesAsync();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<LoanDetailsDto> BuildDetailsDtoAsync(LoanDetails loan, Guid entityId)
    {
        // Resolve account names from DB
        var accountIds = new List<Guid> { loan.AccountId };
        if (loan.PropertyAccountId.HasValue) accountIds.Add(loan.PropertyAccountId.Value);
        if (loan.OffsetAccountId.HasValue) accountIds.Add(loan.OffsetAccountId.Value);

        var accounts = await _db.Accounts
            .Where(a => accountIds.Contains(a.Id) && !a.IsDeleted)
            .ToDictionaryAsync(a => a.Id, a => a.Name);

        return new LoanDetailsDto
        {
            Id = loan.Id,
            AccountId = loan.AccountId,
            AccountName = accounts.GetValueOrDefault(loan.AccountId, ""),
            EntityId = loan.EntityId,
            PropertyAccountId = loan.PropertyAccountId,
            PropertyAccountName = loan.PropertyAccountId.HasValue ? accounts.GetValueOrDefault(loan.PropertyAccountId.Value) : null,
            OffsetAccountId = loan.OffsetAccountId,
            OffsetAccountName = loan.OffsetAccountId.HasValue ? accounts.GetValueOrDefault(loan.OffsetAccountId.Value) : null,
            LoanAmount = loan.LoanAmount,
            InterestRate = loan.InterestRate,
            RateType = loan.RateType.ToString(),
            FixedRateExpiresAt = loan.FixedRateExpiresAt,
            PaymentFrequency = loan.PaymentFrequency.ToString(),
            RepaymentAmount = loan.RepaymentAmount,
            LoanTermMonths = loan.LoanTermMonths,
            StartDate = loan.StartDate,
            NextPaymentDate = loan.NextPaymentDate,
            IsInterestOnly = loan.IsInterestOnly,
            RedrawAvailable = loan.RedrawAvailable,
            Notes = loan.Notes,
            RateHistory = loan.RateHistory
                .Where(r => !r.IsDeleted)
                .OrderByDescending(r => r.EffectiveFrom)
                .Select(r => new LoanRateHistoryDto
                {
                    Id = r.Id,
                    Rate = r.Rate,
                    EffectiveFrom = r.EffectiveFrom,
                    Notes = r.Notes,
                    CreatedAt = r.CreatedAt
                })
                .ToList(),
            CreatedAt = loan.CreatedAt,
            UpdatedAt = loan.UpdatedAt
        };
    }

    private static decimal GetRateForDate(List<LoanRateHistory> rateHistory, DateTime date, decimal fallbackRate)
    {
        // Most recent rate entry that's effective on or before the given date
        var applicable = rateHistory
            .Where(r => !r.IsDeleted && r.EffectiveFrom <= date)
            .OrderByDescending(r => r.EffectiveFrom)
            .FirstOrDefault();
        return applicable?.Rate ?? fallbackRate;
    }

    private static DateTime CalculateFirstPaymentDate(DateTime startDate, PaymentFrequency frequency)
        => AdvanceDate(startDate, frequency);

    private static DateTime AdvanceDate(DateTime date, PaymentFrequency frequency) => frequency switch
    {
        PaymentFrequency.Weekly => date.AddDays(7),
        PaymentFrequency.Fortnightly => date.AddDays(14),
        _ => date.AddMonths(1)
    };
}
