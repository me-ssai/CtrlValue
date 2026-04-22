using Microsoft.EntityFrameworkCore;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain.Entities;
using CtrlValue.Domain.Enums;
using CtrlValue.Domain.Utilities;
using CtrlValue.Infrastructure.Data;

namespace CtrlValue.Application.Services;

public class AccountService : IAccountService
{
    private readonly AppDbContext _db;

    public AccountService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<AccountDto>> GetAccountsAsync(Guid entityId, AccountType? type = null)
    {
        var query = _db.Accounts.Where(a => a.EntityId == entityId && !a.IsDeleted);

        if (type.HasValue)
            query = query.Where(a => a.AccountType == type.Value);

        return await query
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new AccountDto
            {
                Id = a.Id,
                Name = a.Name,
                AccountType = a.AccountType.ToString(),
                AssetClass = a.AssetClass.HasValue ? a.AssetClass.Value.ToString() : null,
                LiquidityClass = a.LiquidityClass.HasValue ? a.LiquidityClass.Value.ToString() : null,
                Currency = a.Currency,
                Institution = a.Institution,
                AccountNumber = a.AccountNumber,
                Notes = a.Notes,
                IsActive = a.IsActive,
                CreditLimit = a.CreditLimit,
                CurrentBalance = a.CurrentBalance,
                StartingBalance = a.StartingBalance,
                StartingBalanceDate = a.StartingBalanceDate,
                IsOffsetAccount = a.IsOffsetAccount,
                OpenedAt = a.OpenedAt,
                ClosedAt = a.ClosedAt,
                ExternalId = a.ExternalId,
                LastSyncedAt = a.LastSyncedAt,
                CreatedAt = a.CreatedAt,
                UpdatedAt = a.UpdatedAt
            })
            .ToListAsync();
    }

    public async Task<AccountDto> GetAccountByIdAsync(Guid id, Guid entityId)
    {
        var account = await _db.Accounts
            .Where(a => a.Id == id && a.EntityId == entityId)
            .FirstOrDefaultAsync();

        if (account == null)
            throw new KeyNotFoundException($"Account with ID {id} not found or access denied.");

        return new AccountDto
        {
            Id = account.Id,
            Name = account.Name,
            AccountType = account.AccountType.ToString(),
            AssetClass = account.AssetClass.HasValue ? account.AssetClass.Value.ToString() : null,
            LiquidityClass = account.LiquidityClass.HasValue ? account.LiquidityClass.Value.ToString() : null,
            Currency = account.Currency,
            Institution = account.Institution,
            AccountNumber = account.AccountNumber,
            Notes = account.Notes,
            IsActive = account.IsActive,
            CreditLimit = account.CreditLimit,
            CurrentBalance = account.CurrentBalance,
            StartingBalance = account.StartingBalance,
            StartingBalanceDate = account.StartingBalanceDate,
            IsOffsetAccount = account.IsOffsetAccount,
            OpenedAt = account.OpenedAt,
            ClosedAt = account.ClosedAt,
            ExternalId = account.ExternalId,
            LastSyncedAt = account.LastSyncedAt,
            CreatedAt = account.CreatedAt,
            UpdatedAt = account.UpdatedAt
        };
    }

    public async Task<AccountDto> CreateAccountAsync(CreateAccountRequest request, Guid entityId)
    {
        ValidateAccountRequest(request.Name);

        var startingBalanceDate = request.StartingBalanceDate?.ToUniversalTime() ?? DateTime.UtcNow;

        var account = new Account
        {
            EntityId = entityId,
            Name = request.Name.Trim(),
            AccountType = request.AccountType,
            AssetClass = request.AssetClass,
            LiquidityClass = request.LiquidityClass,
            Currency = request.Currency,
            Institution = request.Institution?.Trim(),
            AccountNumber = request.AccountNumber?.Trim(),
            Notes = request.Notes?.Trim(),
            CreditLimit = request.CreditLimit,
            OpenedAt = request.OpenedAt,
            ClosedAt = request.ClosedAt,
            ExternalId = request.ExternalId,
            StartingBalance = request.StartingBalance,
            CurrentBalance = request.StartingBalance,
            StartingBalanceDate = startingBalanceDate,
            IsOffsetAccount = request.IsOffsetAccount,
            TenantId = "default"
        };

        _db.Accounts.Add(account);

        // Always insert an OpeningBalance anchor transaction (even for $0)
        var openingTxn = new Transaction
        {
            EntityId = entityId,
            AccountId = account.Id,
            TxnTime = startingBalanceDate,
            Description = $"Opening Balance as of {startingBalanceDate:yyyy-MM-dd}",
            Amount = request.StartingBalance,
            Currency = request.Currency,
            TxnType = TransactionType.OpeningBalance,
            Direction = TransactionDirection.Inflow,
            IsReconciled = true,
            Source = "SYSTEM"
        };
        _db.Transactions.Add(openingTxn);

        await _db.SaveChangesAsync();

        return new AccountDto
        {
            Id = account.Id,
            Name = account.Name,
            AccountType = account.AccountType.ToString(),
            AssetClass = account.AssetClass.HasValue ? account.AssetClass.Value.ToString() : null,
            LiquidityClass = account.LiquidityClass.HasValue ? account.LiquidityClass.Value.ToString() : null,
            Currency = account.Currency,
            Institution = account.Institution,
            AccountNumber = account.AccountNumber,
            Notes = account.Notes,
            IsActive = account.IsActive,
            CreditLimit = account.CreditLimit,
            CurrentBalance = account.CurrentBalance,
            StartingBalance = account.StartingBalance,
            StartingBalanceDate = account.StartingBalanceDate,
            IsOffsetAccount = account.IsOffsetAccount,
            OpenedAt = account.OpenedAt,
            ClosedAt = account.ClosedAt,
            ExternalId = account.ExternalId,
            LastSyncedAt = account.LastSyncedAt,
            CreatedAt = account.CreatedAt,
            UpdatedAt = account.UpdatedAt
        };
    }

    public async Task<AccountDto> UpdateAccountAsync(Guid id, UpdateAccountRequest request, Guid entityId)
    {
        ValidateAccountRequest(request.Name);

        var account = await _db.Accounts
            .Where(a => a.Id == id && a.EntityId == entityId)
            .FirstOrDefaultAsync();

        if (account == null)
            throw new KeyNotFoundException($"Account with ID {id} not found or access denied.");

        account.Name = request.Name.Trim();
        account.AssetClass = request.AssetClass;
        account.LiquidityClass = request.LiquidityClass;
        account.Currency = request.Currency;
        account.Institution = request.Institution?.Trim();
        account.AccountNumber = request.AccountNumber?.Trim();
        account.Notes = request.Notes?.Trim();
        account.IsActive = request.IsActive;
        account.CreditLimit = request.CreditLimit;
        account.OpenedAt = request.OpenedAt;
        account.ClosedAt = request.ClosedAt;
        account.ExternalId = request.ExternalId;
        account.IsOffsetAccount = request.IsOffsetAccount;

        // Update starting balance anchor if changed
        var newStartingBalanceDate = request.StartingBalanceDate?.ToUniversalTime() ?? account.StartingBalanceDate;
        bool anchorChanged = account.StartingBalance != request.StartingBalance
                          || account.StartingBalanceDate != newStartingBalanceDate;

        if (anchorChanged)
        {
            account.StartingBalance = request.StartingBalance;
            account.StartingBalanceDate = newStartingBalanceDate;

            // Replace the OpeningBalance anchor transaction atomically
            var oldAnchor = await _db.Transactions
                .Where(t => t.AccountId == account.Id && !t.IsDeleted && t.TxnType == TransactionType.OpeningBalance)
                .FirstOrDefaultAsync();

            if (oldAnchor != null)
            {
                oldAnchor.IsDeleted = true;
                oldAnchor.UpdatedAt = DateTime.UtcNow;
            }

            _db.Transactions.Add(new Transaction
            {
                EntityId = account.EntityId,
                AccountId = account.Id,
                TxnTime = newStartingBalanceDate,
                Description = $"Opening Balance as of {newStartingBalanceDate:yyyy-MM-dd}",
                Amount = request.StartingBalance,
                Currency = account.Currency,
                TxnType = TransactionType.OpeningBalance,
                Direction = TransactionDirection.Inflow,
                IsReconciled = true,
                Source = "SYSTEM"
            });
        }

        await _db.SaveChangesAsync();

        return new AccountDto
        {
            Id = account.Id,
            Name = account.Name,
            AccountType = account.AccountType.ToString(),
            AssetClass = account.AssetClass.HasValue ? account.AssetClass.Value.ToString() : null,
            LiquidityClass = account.LiquidityClass.HasValue ? account.LiquidityClass.Value.ToString() : null,
            Currency = account.Currency,
            Institution = account.Institution,
            AccountNumber = account.AccountNumber,
            Notes = account.Notes,
            IsActive = account.IsActive,
            CreditLimit = account.CreditLimit,
            CurrentBalance = account.CurrentBalance,
            StartingBalance = account.StartingBalance,
            StartingBalanceDate = account.StartingBalanceDate,
            IsOffsetAccount = account.IsOffsetAccount,
            OpenedAt = account.OpenedAt,
            ClosedAt = account.ClosedAt,
            ExternalId = account.ExternalId,
            LastSyncedAt = account.LastSyncedAt,
            CreatedAt = account.CreatedAt,
            UpdatedAt = account.UpdatedAt
        };
    }

    public async Task<AccountDeletionImpactDto> GetDeletionImpactAsync(Guid id, Guid entityId)
    {
        var account = await _db.Accounts
            .Where(a => a.Id == id && a.EntityId == entityId && !a.IsDeleted)
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException($"Account with ID {id} not found or access denied.");

        var transactionCount = await _db.Transactions
            .Where(t => !t.IsDeleted && t.AccountId == id)
            .CountAsync();

        var valuationCount = await _db.Valuations
            .Where(v => !v.IsDeleted && v.AccountId == id)
            .CountAsync();

        var positionCount = await _db.Positions
            .Where(p => !p.IsDeleted && p.AccountId == id)
            .CountAsync();

        var hasDepreciationSchedule = await _db.DepreciationSchedules
            .AnyAsync(ds => !ds.IsDeleted && ds.AccountId == id);

        return new AccountDeletionImpactDto
        {
            AccountId = id,
            AccountName = account.Name,
            TransactionCount = transactionCount,
            ValuationCount = valuationCount,
            PositionCount = positionCount,
            HasDepreciationSchedule = hasDepreciationSchedule
        };
    }

    public async Task DeleteAccountAsync(Guid id, Guid entityId)
    {
        var account = await _db.Accounts
            .Where(a => a.Id == id && a.EntityId == entityId)
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException($"Account with ID {id} not found or access denied.");

        var now = DateTime.UtcNow;

        // Cascade soft-delete: transactions
        var transactions = await _db.Transactions
            .Where(t => !t.IsDeleted && t.AccountId == id)
            .ToListAsync();
        foreach (var txn in transactions)
        {
            txn.IsDeleted = true;
            txn.UpdatedAt = now;
        }

        // Cascade soft-delete: valuations
        var valuations = await _db.Valuations
            .Where(v => !v.IsDeleted && v.AccountId == id)
            .ToListAsync();
        foreach (var val in valuations)
        {
            val.IsDeleted = true;
            val.UpdatedAt = now;
        }

        // Cascade soft-delete: positions
        var positions = await _db.Positions
            .Where(p => !p.IsDeleted && p.AccountId == id)
            .ToListAsync();
        foreach (var pos in positions)
        {
            pos.IsDeleted = true;
            pos.UpdatedAt = now;
        }

        // Cascade soft-delete: depreciation schedule
        var deprSchedule = await _db.DepreciationSchedules
            .Where(ds => !ds.IsDeleted && ds.AccountId == id)
            .FirstOrDefaultAsync();
        if (deprSchedule != null)
        {
            deprSchedule.IsDeleted = true;
            deprSchedule.UpdatedAt = now;
        }

        // Soft-delete the account itself
        account.IsDeleted = true;
        account.UpdatedAt = now;

        await _db.SaveChangesAsync();
    }

    public async Task<AccountSummaryDto> GetAccountSummaryAsync(Guid entityId)
    {
        var accounts = await _db.Accounts
            .Where(a => a.EntityId == entityId && a.IsActive && !a.IsDeleted)
            .ToListAsync();

        if (!accounts.Any())
        {
            return new AccountSummaryDto
            {
                TotalAssets = 0,
                TotalLiabilities = 0,
                NetWorth = 0,
                Currency = "AUD",
                AssetCount = 0,
                LiabilityCount = 0,
                Holdings = new List<AccountHoldingDto>()
            };
        }

        var accountIds = accounts.Select(a => a.Id).ToList();

        var positions = await _db.Positions
            .Include(p => p.Instrument)
            .Where(p => accountIds.Contains(p.AccountId) && !p.IsDeleted)
            .ToListAsync();

        var instrumentIds = positions
            .Where(p => p.InstrumentId.HasValue)
            .Select(p => p.InstrumentId!.Value)
            .Distinct()
            .ToList();

        // Build a price map keyed by InstrumentId.
        // Priority: GlobalPriceCache (live API prices) > PriceHistory (manual entries).
        var priceMap = new Dictionary<Guid, decimal>();

        if (instrumentIds.Any())
        {
            var instrumentById = positions
                .Where(p => p.InstrumentId.HasValue && p.Instrument != null)
                .Select(p => p.Instrument!)
                .DistinctBy(i => i.Id)
                .ToDictionary(i => i.Id);

            var symbols = instrumentById.Values
                .Select(i => i.ExternalSymbol ?? i.Symbol)
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct()
                .ToList();

            // 1. Live prices from GlobalPriceCache — AUD entries only.
            // USD-priced entries (non-ASX stocks) are excluded so they don't corrupt AUD valuations.
            var cachedPrices = await _db.GlobalPriceCache
                .Where(g => symbols.Contains(g.Symbol) && g.Currency == "AUD")
                .GroupBy(g => g.Symbol)
                .Select(g => g.OrderByDescending(x => x.AsOfDate).First())
                .ToListAsync();

            var cacheBySymbol = cachedPrices.ToDictionary(g => g.Symbol, g => g.Price);

            // 2. Manual PriceHistory as fallback
            var manualPrices = await _db.PriceHistory
                .Where(ph => instrumentIds.Contains(ph.InstrumentId) && !ph.IsDeleted)
                .GroupBy(ph => ph.InstrumentId)
                .Select(g => g.OrderByDescending(p => p.AsOfDate).First())
                .ToListAsync();

            var manualByInstrument = manualPrices.ToDictionary(p => p.InstrumentId, p => p.ClosePrice);

            foreach (var (instrumentId, instrument) in instrumentById)
            {
                var symbol = instrument.ExternalSymbol ?? instrument.Symbol;
                if (!string.IsNullOrEmpty(symbol) && cacheBySymbol.TryGetValue(symbol, out var cachedPrice))
                    priceMap[instrumentId] = cachedPrice;
                else if (manualByInstrument.TryGetValue(instrumentId, out var manualPrice))
                    priceMap[instrumentId] = manualPrice;
            }
        }

        var positionsByAccount = positions
            .GroupBy(p => p.AccountId)
            .ToDictionary(g => g.Key, g => g.ToList());

        decimal totalAssets = 0;
        decimal totalLiabilities = 0;
        var holdings = new List<AccountHoldingDto>();

        foreach (var account in accounts)
        {
            decimal value;

            positionsByAccount.TryGetValue(account.Id, out var accountPositions);
            accountPositions ??= new List<Position>();

            decimal positionValue = 0;

            foreach (var pos in accountPositions)
            {
                if (pos.InstrumentId.HasValue &&
                    priceMap.TryGetValue(pos.InstrumentId.Value, out var price))
                {
                    // Convert position quantity to the instrument's price unit (e.g. g → troy oz)
                    // before multiplying by price, exactly as PositionService does.
                    var priceUnit = pos.Instrument?.PriceUnit ?? MetalUnit.UNIT;
                    var convertedQty = MetalUnitConverter.Convert(pos.Quantity, pos.Unit, priceUnit);
                    positionValue += convertedQty * price;
                }
                else
                {
                    positionValue += pos.CostBasisTotal ?? 0;
                }
            }

            if (account.AccountType == AccountType.LIABILITY)
            {
                // Store liabilities as positive owed amount for summary purposes
                value = Math.Abs(account.CurrentBalance);
                totalLiabilities += value;
            }
            else
            {
                // Investment-like assets: balance + positions
                // CurrentBalance represents the cash component.
                value = account.CurrentBalance + positionValue;
                totalAssets += value;
            }

            holdings.Add(new AccountHoldingDto
            {
                AccountId = account.Id,
                AccountName = account.Name,
                AccountType = account.AccountType.ToString(),
                Institution = account.Institution,
                Currency = account.Currency,
                AssetClass = account.AssetClass.HasValue ? account.AssetClass.Value.ToString() : null,
                LiquidityClass = account.LiquidityClass.HasValue ? account.LiquidityClass.Value.ToString() : null,
                Value = value
            });
        }

        return new AccountSummaryDto
        {
            TotalAssets = totalAssets,
            TotalLiabilities = totalLiabilities,
            NetWorth = totalAssets - totalLiabilities,
            Currency = "AUD",
            AssetCount = accounts.Count(a => a.AccountType == AccountType.ASSET),
            LiabilityCount = accounts.Count(a => a.AccountType == AccountType.LIABILITY),
            Holdings = holdings
        };
    }

    // public async Task RecalculateBalanceAsync(Guid accountId, Guid entityId)
    // {
    //     var account = await _db.Accounts
    //         .FirstOrDefaultAsync(a => a.Id == accountId && a.EntityId == entityId);

    //     if (account == null) return;

    //     // Sum transactions from zero
    //     var transactions = await _db.Transactions
    //         .Where(t => t.EntityId == entityId && !t.IsDeleted &&
    //                     (t.FromAccountId == accountId || t.ToAccountId == accountId))
    //         .ToListAsync();

    //     decimal balance = 0m;

    //     foreach (var txn in transactions)
    //     {
    //         decimal amount = txn.Amount;

    //         bool isFrom = txn.FromAccountId == accountId;
    //         bool isTo = txn.ToAccountId == accountId;

    //         switch (txn.TxnType)
    //         {
    //             case TransactionType.Income:
    //             case TransactionType.CapitalDeposit:
    //                 if (isTo) balance += amount;
    //                 break;

    //             case TransactionType.Expense:
    //             case TransactionType.CapitalWithdrawal:
    //                 if (isFrom)
    //                 {
    //                     if (account.AccountType == AccountType.ASSET)
    //                         balance -= amount;
    //                     else
    //                         balance += amount; // Spending from credit card increases debt
    //                 }
    //                 break;

    //             case TransactionType.Transfer:
    //                 if (isFrom)
    //                 {
    //                     if (account.AccountType == AccountType.LIABILITY)
    //                         balance += amount; // Drawing from credit -> more debt
    //                     else
    //                         balance -= amount; // Paying out of asset
    //                 }
    //                 if (isTo)
    //                 {
    //                     if (account.AccountType == AccountType.LIABILITY)
    //                         balance -= amount; // Paying toward liability -> less debt
    //                     else
    //                         balance += amount; // Receiving into asset
    //                 }
    //                 break;

    //             case TransactionType.AssetPurchase:
    //                 if (isFrom) balance -= amount;
    //                 if (isTo && !txn.InstrumentId.HasValue) balance += amount; // Physical asset
    //                 break;

    //             case TransactionType.AssetSale:
    //                 if (isFrom && !txn.InstrumentId.HasValue) balance -= amount; // Physical asset
    //                 if (isTo) balance += amount; // Cash received
    //                 break;

    //             case TransactionType.LoanDisbursement:
    //                 if (isTo) balance += amount; // Receive cash (Asset +)
    //                 if (isFrom) balance += amount; // Increase debt (Liability +)
    //                 break;

    //             case TransactionType.LoanRepayment:
    //                 if (isFrom) balance -= amount; // Pay cash (Asset -)
    //                 if (isTo) balance -= amount;   // Decrease debt (Liability -)
    //                 break;
    //         }
    //     }

    //     account.CurrentBalance = balance;
    //     await _db.SaveChangesAsync();
    // }

    public async Task RecalculateBalanceAsync(Guid accountId, Guid entityId)
    {
        var account = await _db.Accounts
            .FirstOrDefaultAsync(a => a.Id == accountId && a.EntityId == entityId);

        if (account == null) return;

        var cutoff = account.StartingBalanceDate;

        // Only include transactions on or after the starting balance date,
        // and exclude the OpeningBalance anchor transaction (it is read from the Account field).
        var incoming = await _db.Transactions
            .Where(t => t.EntityId == entityId
                     && !t.IsDeleted
                     && t.AccountId == accountId
                     && t.TxnType != TransactionType.OpeningBalance
                     && t.TxnTime >= cutoff
                     && t.Direction == TransactionDirection.Inflow)
            .SumAsync(t => (decimal?)t.Amount) ?? 0m;

        var outgoing = await _db.Transactions
            .Where(t => t.EntityId == entityId
                     && !t.IsDeleted
                     && t.AccountId == accountId
                     && t.TxnType != TransactionType.OpeningBalance
                     && t.TxnTime >= cutoff
                     && t.Direction == TransactionDirection.Outflow)
            .SumAsync(t => (decimal?)t.Amount) ?? 0m;

        account.CurrentBalance = account.StartingBalance + incoming - outgoing;

        await _db.SaveChangesAsync();
    }

    private static void ValidateAccountRequest(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Account name is required.");
    }

    public async Task RecalculateAllBalancesAsync(Guid entityId)
    {
        var accountIds = await _db.Accounts
            .Where(a => a.EntityId == entityId && !a.IsDeleted)
            .Select(a => a.Id)
            .ToListAsync();

        foreach (var accountId in accountIds)
        {
            await RecalculateBalanceAsync(accountId, entityId);
        }
    }
}
