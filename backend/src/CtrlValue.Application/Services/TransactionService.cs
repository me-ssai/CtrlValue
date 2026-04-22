using Microsoft.EntityFrameworkCore;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain.Entities;
using CtrlValue.Domain.Enums;
using CtrlValue.Infrastructure.Data;

namespace CtrlValue.Application.Services;

public class TransactionService : ITransactionService
{
    private readonly AppDbContext _db;
    private readonly IAccountService _accountService;

    public TransactionService(AppDbContext db, IAccountService accountService)
    {
        _db = db;
        _accountService = accountService;
    }

    public async Task<List<TransactionDto>> GetTransactionsAsync(Guid entityId, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _db.Transactions
            .Include(t => t.Account)
            .Include(t => t.Instrument)
            .Include(t => t.Category)
            .Where(t => t.EntityId == entityId && !t.IsDeleted);

        if (startDate.HasValue)
            query = query.Where(t => t.TxnTime >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(t => t.TxnTime <= endDate.Value);

        return await query
            .OrderByDescending(t => t.TxnTime)
            .ThenByDescending(t => t.CreatedAt)
            .Select(t => new TransactionDto
            {
                Id = t.Id,
                TxnTime = t.TxnTime,
                Description = t.Description,
                TxnType = t.TxnType.ToString(),
                AccountId = t.AccountId,
                AccountName = t.Account != null ? t.Account.Name : null,
                Direction = t.Direction.ToString(),
                Amount = t.Amount,
                Currency = t.Currency,
                InstrumentId = t.InstrumentId,
                InstrumentSymbol = t.Instrument != null ? t.Instrument.Symbol : null,
                Quantity = t.Quantity,
                UnitPrice = t.UnitPrice,
                Fees = t.Fees,
                CategoryId = t.CategoryId,
                CategoryName = t.Category != null ? t.Category.Name : null,
                Merchant = t.Merchant,
                IsReconciled = t.IsReconciled,
                IsTaxDeductible = t.IsTaxDeductible,
                Tags = t.Tags,
                ReceiptUrl = t.ReceiptUrl,
                ExternalId = t.ExternalId,
                RelatedTxnId = t.RelatedTxnId,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            })
            .ToListAsync();
    }

    public async Task<List<TransactionDto>> GetTransactionsByAccountAsync(Guid accountId, Guid entityId)
    {
        return await _db.Transactions
            .Include(t => t.Account)
            .Include(t => t.Instrument)
            .Include(t => t.Category)
            .Where(t => t.EntityId == entityId
                        && !t.IsDeleted
                        && t.AccountId == accountId)
            .OrderByDescending(t => t.TxnTime)
            .ThenByDescending(t => t.CreatedAt)
            .Select(t => new TransactionDto
            {
                Id = t.Id,
                TxnTime = t.TxnTime,
                Description = t.Description,
                TxnType = t.TxnType.ToString(),
                AccountId = t.AccountId,
                AccountName = t.Account != null ? t.Account.Name : null,
                Direction = t.Direction.ToString(),
                Amount = t.Amount,
                Currency = t.Currency,
                InstrumentId = t.InstrumentId,
                InstrumentSymbol = t.Instrument != null ? t.Instrument.Symbol : null,
                Quantity = t.Quantity,
                UnitPrice = t.UnitPrice,
                Fees = t.Fees,
                CategoryId = t.CategoryId,
                CategoryName = t.Category != null ? t.Category.Name : null,
                Merchant = t.Merchant,
                IsReconciled = t.IsReconciled,
                IsTaxDeductible = t.IsTaxDeductible,
                Tags = t.Tags,
                ReceiptUrl = t.ReceiptUrl,
                ExternalId = t.ExternalId,
                RelatedTxnId = t.RelatedTxnId,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            })
            .ToListAsync();
    }

    public async Task<TransactionDto> GetTransactionByIdAsync(Guid id, Guid entityId)
    {
        var transaction = await _db.Transactions
            .Include(t => t.Account)
            .Include(t => t.Instrument)
            .Include(t => t.Category)
            .Where(t => t.Id == id && t.EntityId == entityId)
            .FirstOrDefaultAsync();

        if (transaction == null)
            throw new KeyNotFoundException($"Transaction with ID {id} not found or access denied.");

        return MapToDto(transaction);
    }

    public async Task<TransactionDto> CreateTransactionAsync(CreateTransactionRequest request, Guid entityId)
    {
        ValidateTransactionResult(request.Amount, request.Description);

        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.Id == request.AccountId && a.EntityId == entityId);
        if (account == null)
            throw new ArgumentException("Account not found.");

        ValidateTransactionLogic(request.TxnType, account);

        // Core transaction row
        var transaction = new Transaction
        {
            EntityId = entityId,
            TxnTime = request.TxnTime,
            Description = request.Description.Trim(),
            TxnType = request.TxnType,
            AccountId = request.AccountId,
            Direction = request.Direction,
            Amount = request.Amount,
            Currency = request.Currency,
            InstrumentId = request.InstrumentId,
            Quantity = request.Quantity,
            UnitPrice = request.UnitPrice,
            Fees = request.Fees,
            CategoryId = request.CategoryId,
            Merchant = request.Merchant?.Trim(),
            IsTaxDeductible = request.IsTaxDeductible,
            Tags = request.Tags,
            ReceiptUrl = request.ReceiptUrl,
            ExternalId = request.ExternalId,
            RelatedTxnId = request.RelatedTxnId,
        };

        _db.Transactions.Add(transaction);

        // Handle Internal Transfer (generate counter row)
        if (request.TxnType == TransactionType.Transfer && request.CounterAccountId.HasValue)
        {
            var counterAccount = await _db.Accounts
                .FirstOrDefaultAsync(a => a.Id == request.CounterAccountId.Value && a.EntityId == entityId);

            if (counterAccount == null)
                throw new ArgumentException("Counter account not found.");

            var transferGroupId = Guid.NewGuid();

            // Original transaction = expense / money leaving selected account
            transaction.TransferGroupId = transferGroupId;
            transaction.TxnType = TransactionType.Expense;
            transaction.Direction = TransactionDirection.Outflow;

            var counterTxn = new Transaction
            {
                EntityId = entityId,
                TxnTime = request.TxnTime,
                Description = request.Description.Trim(),
                AccountId = request.CounterAccountId.Value,

                // Counter transaction = income / money entering counter account
                TxnType = TransactionType.Income,
                Direction = TransactionDirection.Inflow,

                Amount = request.Amount,
                Currency = request.Currency,
                TransferGroupId = transferGroupId,

                // Optional but recommended if you already support this
                RelatedTxnId = transaction.Id
            };

            _db.Transactions.Add(counterTxn);
        }

        await ApplyTransactionEffectAsync(transaction, account, reverse: false);
        await _db.SaveChangesAsync();

        await _accountService.RecalculateBalanceAsync(transaction.AccountId, entityId);
        if (request.CounterAccountId.HasValue)
            await _accountService.RecalculateBalanceAsync(request.CounterAccountId.Value, entityId);

        await _db.Entry(transaction).Reference(t => t.Account).LoadAsync();
        await _db.Entry(transaction).Reference(t => t.Instrument).LoadAsync();
        await _db.Entry(transaction).Reference(t => t.Category).LoadAsync();

        return MapToDto(transaction);
    }

    public async Task<TransactionDto> UpdateTransactionAsync(Guid id, UpdateTransactionRequest request, Guid entityId)
    {
        ValidateTransactionResult(request.Amount, request.Description);

        var transaction = await _db.Transactions
            .Include(t => t.Account)
            .FirstOrDefaultAsync(t => t.Id == id && t.EntityId == entityId);

        if (transaction == null)
            throw new KeyNotFoundException($"Transaction with ID {id} not found or access denied.");

        await ApplyTransactionEffectAsync(transaction, transaction.Account, reverse: true);

        var oldAccountId = transaction.AccountId;

        transaction.TxnTime = request.TxnTime;
        transaction.Description = request.Description.Trim();
        transaction.TxnType = request.TxnType;
        transaction.AccountId = request.AccountId;
        transaction.Amount = request.Amount;
        transaction.Currency = request.Currency;
        transaction.InstrumentId = request.InstrumentId;
        transaction.Quantity = request.Quantity;
        transaction.UnitPrice = request.UnitPrice;
        transaction.Fees = request.Fees;
        transaction.CategoryId = request.CategoryId;
        transaction.Merchant = request.Merchant?.Trim();
        transaction.IsTaxDeductible = request.IsTaxDeductible;
        transaction.Tags = request.Tags;
        transaction.ReceiptUrl = request.ReceiptUrl;
        transaction.ExternalId = request.ExternalId;
        transaction.RelatedTxnId = request.RelatedTxnId;
        transaction.Direction = request.Direction;

        var newAccount = await _db.Accounts.FirstOrDefaultAsync(a => a.Id == request.AccountId && a.EntityId == entityId);
        ValidateTransactionLogic(request.TxnType, newAccount);

        await ApplyTransactionEffectAsync(transaction, newAccount, reverse: false);
        await _db.SaveChangesAsync();

        var accountsToRecalculate = new HashSet<Guid> { oldAccountId, transaction.AccountId };
        
        // Also update counter transaction if it's a transfer part
        if (transaction.TxnType == TransactionType.Transfer && transaction.TransferGroupId.HasValue)
        {
             // For now, updating one leg of a transfer only updates that leg's time/amount
             // Could be expanded to update both legs sync
             var counter = await _db.Transactions
                 .FirstOrDefaultAsync(t => t.TransferGroupId == transaction.TransferGroupId && t.Id != transaction.Id);
             
             if (counter != null)
             {
                 counter.Amount = transaction.Amount;
                 counter.TxnTime = transaction.TxnTime;
                 counter.Currency = transaction.Currency;
                 accountsToRecalculate.Add(counter.AccountId);
             }
             await _db.SaveChangesAsync();
        }

        foreach (var accId in accountsToRecalculate)
        {
            await _accountService.RecalculateBalanceAsync(accId, entityId);
        }

        await _db.Entry(transaction).Reference(t => t.Account).LoadAsync();
        await _db.Entry(transaction).Reference(t => t.Instrument).LoadAsync();
        await _db.Entry(transaction).Reference(t => t.Category).LoadAsync();

        return MapToDto(transaction);
    }

    private async Task SoftDeleteTransactionInternalAsync(Transaction transaction)
    {
        await ApplyTransactionEffectAsync(transaction, transaction.Account, reverse: true);
        transaction.IsDeleted = true;
        transaction.UpdatedAt = DateTime.UtcNow;
    }

    public async Task DeleteTransactionAsync(Guid id, Guid entityId)
    {
        var transaction = await _db.Transactions
            .Include(t => t.Account)
            .FirstOrDefaultAsync(t => t.Id == id && t.EntityId == entityId && !t.IsDeleted);

        if (transaction == null)
            throw new KeyNotFoundException($"Transaction with ID {id} not found or access denied.");

        var accountsToRecalculate = new HashSet<Guid> { transaction.AccountId };

        await SoftDeleteTransactionInternalAsync(transaction);

        // Delete linked transfer counterpart if it exists
        if (transaction.TransferGroupId.HasValue)
        {
            var counter = await _db.Transactions
                .Include(t => t.Account)
                .FirstOrDefaultAsync(t => t.TransferGroupId == transaction.TransferGroupId && t.Id != transaction.Id && !t.IsDeleted);

            if (counter != null)
            {
                await SoftDeleteTransactionInternalAsync(counter);
                accountsToRecalculate.Add(counter.AccountId);
            }
        }

        await _db.SaveChangesAsync();

        foreach (var accId in accountsToRecalculate)
        {
            await _accountService.RecalculateBalanceAsync(accId, entityId);
        }
    }

    public async Task BulkDeleteTransactionsAsync(List<Guid> transactionIds, Guid entityId)
    {
        if (transactionIds == null || transactionIds.Count == 0)
            throw new ArgumentException("No transaction IDs were provided.");

        var distinctIds = transactionIds.Distinct().ToList();

        await using var dbTransaction = await _db.Database.BeginTransactionAsync();

        var transactions = await _db.Transactions
            .Include(t => t.Account)
            .Where(t => distinctIds.Contains(t.Id) && t.EntityId == entityId && !t.IsDeleted)
            .ToListAsync();

        if (transactions.Count != distinctIds.Count)
            throw new KeyNotFoundException("One or more transactions were not found or access was denied.");

        var accountsToRecalculate = new HashSet<Guid>();
        
        foreach (var transaction in transactions)
        {
            await SoftDeleteTransactionInternalAsync(transaction);
            accountsToRecalculate.Add(transaction.AccountId);

            if (transaction.TransferGroupId.HasValue)
            {
                var counters = await _db.Transactions
                    .Where(t => t.TransferGroupId == transaction.TransferGroupId && t.Id != transaction.Id && !t.IsDeleted)
                    .ToListAsync();
                    
                foreach(var c in counters)
                {
                     await SoftDeleteTransactionInternalAsync(c);
                     accountsToRecalculate.Add(c.AccountId);
                }
            }
        }

        await _db.SaveChangesAsync();
        await dbTransaction.CommitAsync();

        foreach (var accId in accountsToRecalculate)
        {
            await _accountService.RecalculateBalanceAsync(accId, entityId);
        }
    }

    private static void ValidateTransactionResult(decimal amount, string description)
    {
        if (amount < 0)
            throw new ArgumentException("Transaction amount cannot be negative.");
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Transaction description is required.");
    }

    private static void ValidateTransactionLogic(TransactionType type, Account? account)
    {
        if (account == null)
            throw new ArgumentException("Transaction must be tied to an account.");

        switch (type)
        {
            case TransactionType.Income:
            case TransactionType.CapitalDeposit:
                 // Income goes to Asset
                 break;
            case TransactionType.Expense:
            case TransactionType.CapitalWithdrawal:
                 break;
            case TransactionType.AssetPurchase:
            case TransactionType.AssetSale:
                 break;
        }
    }

    private async Task ApplyTransactionEffectAsync(Transaction txn, Account? account, bool reverse)
    {
        decimal factor = reverse ? -1 : 1;
        decimal amount = txn.Amount;

        switch (txn.TxnType)
        {
            case TransactionType.AssetPurchase:
                if (txn.InstrumentId.HasValue && txn.Direction == TransactionDirection.Outflow)
                {
                    var costIncludingFees = amount + (txn.Fees ?? 0);
                    await UpdatePositionAsync(
                        txn.InstrumentId.Value,
                        account?.Id,
                        txn.EntityId,
                        txn.Quantity ?? 0,
                        costIncludingFees,
                        factor,
                        useAverageCost: false,
                        openedAt: txn.TxnTime);
                }
                break;

            case TransactionType.AssetSale:
                if (txn.InstrumentId.HasValue && txn.Direction == TransactionDirection.Inflow)
                {
                    await UpdatePositionAsync(
                        txn.InstrumentId.Value,
                        account?.Id,
                        txn.EntityId,
                        -(txn.Quantity ?? 0),
                        0,
                        factor,
                        useAverageCost: true,
                        openedAt: txn.TxnTime);
                }
                break;
        }
    }

    private async Task UpdatePositionAsync(
        Guid instrumentId,
        Guid? accountId,
        Guid entityId,
        decimal qtyDelta,
        decimal specificCostDelta,
        decimal factor,
        bool useAverageCost,
        DateTime? openedAt = null)
    {
        if (!accountId.HasValue) return;

        var position = await _db.Positions
            .FirstOrDefaultAsync(p => p.AccountId == accountId && p.InstrumentId == instrumentId);

        decimal finalQtyDelta = qtyDelta * factor;
        decimal finalCostDelta = specificCostDelta * factor;

        if (position == null)
        {
            if (finalQtyDelta <= 0) return;
            position = new Position
            {
                AccountId      = accountId.Value,
                InstrumentId   = instrumentId,
                Quantity       = 0,
                CostBasisTotal = 0,
                OpenedAt       = openedAt?.ToUniversalTime() ?? DateTime.UtcNow
            };
            _db.Positions.Add(position);
        }

        if (useAverageCost)
        {
            if (position.Quantity != 0)
            {
                decimal averageCost = (position.CostBasisTotal ?? 0) / position.Quantity;
                decimal costAdjustment = finalQtyDelta * averageCost;
                position.CostBasisTotal = (position.CostBasisTotal ?? 0) + costAdjustment;
            }
        }
        else
        {
            position.CostBasisTotal = (position.CostBasisTotal ?? 0) + finalCostDelta;
        }

        position.Quantity += finalQtyDelta;
    }

    private static TransactionDto MapToDto(Transaction t)
    {
        return new TransactionDto
        {
            Id = t.Id,
            TxnTime = t.TxnTime,
            Description = t.Description,
            TxnType = t.TxnType.ToString(),
            AccountId = t.AccountId,
            AccountName = t.Account?.Name,
            Direction = t.Direction.ToString(),
            Amount = t.Amount,
            Currency = t.Currency,
            InstrumentId = t.InstrumentId,
            InstrumentSymbol = t.Instrument?.Symbol,
            Quantity = t.Quantity,
            UnitPrice = t.UnitPrice,
            Fees = t.Fees,
            CategoryId = t.CategoryId,
            CategoryName = t.Category?.Name,
            Merchant = t.Merchant,
            IsReconciled = t.IsReconciled,
            IsTaxDeductible = t.IsTaxDeductible,
            Tags = t.Tags,
            ReceiptUrl = t.ReceiptUrl,
            ExternalId = t.ExternalId,
            RelatedTxnId = t.RelatedTxnId,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt
        };
    }
}
