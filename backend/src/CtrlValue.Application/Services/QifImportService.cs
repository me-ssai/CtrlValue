using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain.Entities;
using CtrlValue.Domain.Enums;
using CtrlValue.Infrastructure.Data;

namespace CtrlValue.Application.Services;

public class QifImportService : IQifImportService
{
    private readonly AppDbContext _db;
    private readonly IAiCategorizationService _aiCategorization;
    private readonly IAccountService _accountService;
    private readonly ICategoryKeywordRuleService _keywordRuleService;
    private readonly IAccountKeywordRuleService _accountKeywordRuleService;

    public QifImportService(AppDbContext db, IAiCategorizationService aiCategorization, IAccountService accountService, ICategoryKeywordRuleService keywordRuleService, IAccountKeywordRuleService accountKeywordRuleService)
    {
        _db = db;
        _aiCategorization = aiCategorization;
        _accountService = accountService;
        _keywordRuleService = keywordRuleService;
        _accountKeywordRuleService = accountKeywordRuleService;
    }

    public async Task<ImportedTransactionsFileDto> UploadAndStageAsync(
        Guid entityId,
        Guid accountId,
        bool allowDuplicates,
        string? dateFormat,
        Stream fileStream,
        string filename)
    {
        var account = await _db.Accounts
            .FirstOrDefaultAsync(a => a.Id == accountId && a.EntityId == entityId)
            ?? throw new KeyNotFoundException("Account not found or access denied.");

        var importFile = new ImportedTransactionsFile
        {
            EntityId = entityId,
            AccountId = accountId,
            OriginalFilename = filename,
            UploadedAt = DateTime.UtcNow,
            Status = ImportStatus.Staged,
            AllowDuplicates = allowDuplicates,
            TenantId = account.TenantId
        };
        _db.ImportedTransactionsFiles.Add(importFile);
        await _db.SaveChangesAsync();

        var parsedRows = ParseQif(fileStream, importFile, account.TenantId, dateFormat);

        if (parsedRows.Count == 0)
        {
            importFile.Status = ImportStatus.Failed;
            await _db.SaveChangesAsync();
            return MapFileToDto(importFile, account.Name);
        }

        var existingHashList = await _db.Transactions
            .Where(t => t.EntityId == entityId && !t.IsDeleted)
            .Select(t => t.ExternalId)
            .Where(h => h != null)
            .ToListAsync();
        var committedHashes = existingHashList.ToHashSet()!;

        foreach (var row in parsedRows.Where(r => r.Status == StagingStatus.Valid))
        {
            if (committedHashes.Contains(row.Hash))
                row.Status = StagingStatus.AlreadyImported;
        }

        // Secondary check: IMPORT_COUNTER transactions have no ExternalId so the hash
        // check above misses them. Match by (date, amount, direction) on this account.
        var counterLegs = await _db.Transactions
            .Where(t => t.EntityId == entityId
                     && t.AccountId == accountId
                     && t.Source == "IMPORT_COUNTER"
                     && !t.IsDeleted)
            .Select(t => new { t.TxnTime, t.Amount, t.Direction })
            .ToListAsync();

        var counterLegKeys = counterLegs
            .Select(t => $"{t.TxnTime:yyyy-MM-dd}|{t.Amount:F2}|{t.Direction}")
            .ToHashSet();

        foreach (var row in parsedRows.Where(r => r.Status == StagingStatus.Valid))
        {
            var dir = row.AmountRaw > 0 ? TransactionDirection.Inflow : TransactionDirection.Outflow;
            if (counterLegKeys.Contains($"{row.TransactionDate:yyyy-MM-dd}|{row.Amount:F2}|{dir}"))
                row.Status = StagingStatus.AlreadyImported;
        }

        if (!allowDuplicates)
        {
            var batchHashes = new HashSet<string>();
            foreach (var row in parsedRows.Where(r => r.Status == StagingStatus.Valid))
            {
                if (!batchHashes.Add(row.Hash))
                    row.Status = StagingStatus.Duplicate;
            }
        }

        foreach (var row in parsedRows)
        {
            if (row.Status != StagingStatus.Valid) 
                continue;

            var amt = row.AmountRaw;

            if (amt == 0)
            {
                row.Status = StagingStatus.Error;
                row.ErrorReason = "Amount is zero; cannot determine direction.";
            }

            row.AccountId = accountId;
        }

        _db.ImportedTransactionsFilesStaging.AddRange(parsedRows);

        importFile.TotalRows = parsedRows.Count;
        importFile.ValidRows = parsedRows.Count(r => r.Status == StagingStatus.Valid);
        importFile.DuplicateRows = parsedRows.Count(r => r.Status == StagingStatus.Duplicate);
        importFile.AlreadyImportedRows = parsedRows.Count(r => r.Status == StagingStatus.AlreadyImported);
        importFile.ErrorRows = parsedRows.Count(r => r.Status == StagingStatus.Error);

        await _db.SaveChangesAsync();

        var validRows = parsedRows
            .Where(r => r.Status == StagingStatus.Valid)
            .ToList();

        if (validRows.Count > 0)
        {
            // 1. Rules-based auto-categorisation
            foreach (var row in validRows)
            {
                var matchedCategoryId = await _keywordRuleService.MatchCategoryAsync(entityId, row.Description);
                if (matchedCategoryId != null)
                    row.CategoryId = matchedCategoryId;
            }

            // 2. Rules-based counter-account auto-population
            foreach (var row in validRows.Where(r => r.CounterAccountId == null))
            {
                var matchedAccountId = await _accountKeywordRuleService.MatchAccountAsync(entityId, row.Description);
                if (matchedAccountId.HasValue && matchedAccountId.Value != row.AccountId)
                    row.CounterAccountId = matchedAccountId;
            }

            // 3. AI Categorisation for remaining uncategorised rows
            var uncategorisedRows = validRows.Where(r => r.CategoryId == null).ToList();
            if (uncategorisedRows.Count > 0)
            {
                var tenantCategories = await _db.Categories
                    .Include(c => c.ParentCategory)
                    .Where(c => c.EntityId == entityId && c.IsActive)
                    .ToListAsync();

                await _aiCategorization.CategorizeAsync(uncategorisedRows, tenantCategories);
            }

            await _db.SaveChangesAsync(); 
        }

        return MapFileToDto(importFile, account.Name);
    }

    public async Task<StagedImportReviewDto> GetStagedImportAsync(Guid fileId, Guid entityId)
    {
        var importFile = await _db.ImportedTransactionsFiles
            .Include(f => f.Account)
            .FirstOrDefaultAsync(f => f.Id == fileId && f.EntityId == entityId)
            ?? throw new KeyNotFoundException("Import file not found or access denied.");

        var stagingRows = await _db.ImportedTransactionsFilesStaging
            .Where(s => s.ImportedTransactionsFileId == fileId)
            .Include(s => s.PrimaryAccount)
            .Include(s => s.CounterAccount)
            .Include(s => s.Category)
            .OrderBy(s => s.TransactionDate)
            .ToListAsync();

        return new StagedImportReviewDto
        {
            File = MapFileToDto(importFile, importFile.Account?.Name),
            ValidRows = stagingRows.Where(s => s.Status == StagingStatus.Valid).Select(s => MapRowToDto(s, ComputeInferredType(s))).ToList(),
            DuplicateRows = stagingRows.Where(s => s.Status == StagingStatus.Duplicate).Select(s => MapRowToDto(s, ComputeInferredType(s))).ToList(),
            AlreadyImportedRows = stagingRows.Where(s => s.Status == StagingStatus.AlreadyImported).Select(s => MapRowToDto(s, ComputeInferredType(s))).ToList(),
            ErrorRows = stagingRows.Where(s => s.Status == StagingStatus.Error).Select(s => MapRowToDto(s, ComputeInferredType(s))).ToList()
        };
    }

    public async Task<StagingRowDto> UpdateStagingRowAsync(
        Guid fileId,
        Guid rowId,
        UpdateStagingRowRequest request,
        Guid entityId)
    {
        var row = await _db.ImportedTransactionsFilesStaging
            .FirstOrDefaultAsync(s => s.Id == rowId && s.ImportedTransactionsFileId == fileId)
            ?? throw new KeyNotFoundException("Staging row not found.");

        var fileExists = await _db.ImportedTransactionsFiles
            .AnyAsync(f => f.Id == fileId && f.EntityId == entityId);
        if (!fileExists)
            throw new KeyNotFoundException("Import file not found or access denied.");

        if (request.CounterAccountId.HasValue)
        {
            var counterAccountExists = await _db.Accounts
                .AnyAsync(a => a.Id == request.CounterAccountId.Value && a.EntityId == entityId);
            if (!counterAccountExists)
                throw new ArgumentException("CounterAccount not found or access denied.");

            if (row.AccountId == request.CounterAccountId.Value)
                throw new ArgumentException("Counter account cannot be the same as the primary import account.");
        }

        row.CounterAccountId = request.CounterAccountId;
        row.CategoryId = request.CategoryId;

        if (request.IgnoreDuplicateWarning == true)
        {
            var oldStatus = row.Status;
            if (oldStatus == StagingStatus.Duplicate || oldStatus == StagingStatus.AlreadyImported)
            {
                row.Status = StagingStatus.Valid;

                var file = await _db.ImportedTransactionsFiles.FindAsync(fileId);
                if (file != null)
                {
                    if (oldStatus == StagingStatus.Duplicate) file.DuplicateRows--;
                    else if (oldStatus == StagingStatus.AlreadyImported) file.AlreadyImportedRows--;
                    file.ValidRows++;
                }
            }
        }

        row.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await _db.Entry(row).Reference(r => r.PrimaryAccount).LoadAsync();
        await _db.Entry(row).Reference(r => r.CounterAccount).LoadAsync();
        await _db.Entry(row).Reference(r => r.Category).LoadAsync();

        return MapRowToDto(row, ComputeInferredType(row));
    }

    public async Task<ImportedTransactionsFileDto> CommitImportAsync(Guid fileId, Guid entityId)
    {
        var importFile = await _db.ImportedTransactionsFiles
            .Include(f => f.Account)
            .FirstOrDefaultAsync(f => f.Id == fileId && f.EntityId == entityId)
            ?? throw new KeyNotFoundException("Import file not found or access denied.");

        if (importFile.Status == ImportStatus.Confirmed)
            throw new InvalidOperationException("Import file has already been committed.");

        var validRows = await _db.ImportedTransactionsFilesStaging
            .Where(s => s.ImportedTransactionsFileId == fileId && s.Status == StagingStatus.Valid)
            .ToListAsync();

        await using var dbTransaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var stagingAccountIds = validRows
                .SelectMany(r => new Guid?[] { r.AccountId, r.CounterAccountId })
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            var accounts = await _db.Accounts
                .Where(a => stagingAccountIds.Contains(a.Id) && a.EntityId == entityId)
                .ToDictionaryAsync(a => a.Id);



            var newTransactions = new List<Transaction>();

            foreach (var row in validRows)
            {
                var direction = row.AmountRaw > 0 ? TransactionDirection.Inflow : TransactionDirection.Outflow;
                bool isInternal = row.CounterAccountId.HasValue;

                Guid accountId = row.AccountId;
                Guid? counterAccountId = row.CounterAccountId;
                TransactionType txnType;
                Guid? transferGroupId = null;

                if (isInternal)
                {
                    txnType = ResolveInternalTxnType(accounts, accountId, counterAccountId!.Value, direction);
                    transferGroupId = Guid.NewGuid();
                }
                else if (direction == TransactionDirection.Inflow)
                {
                    txnType = TransactionType.Income;
                }
                else
                {
                    txnType = TransactionType.Expense;
                }

                var txn = new Transaction
                {
                    EntityId                 = entityId,
                    TxnTime                 = DateTime.SpecifyKind(row.TransactionDate, DateTimeKind.Utc),
                    Description             = row.Description,
                    TxnType                 = txnType,
                    AccountId               = accountId,
                    Amount                  = row.Amount,
                    Currency                = importFile.Account?.Currency ?? "AUD",
                    CategoryId              = row.CategoryId,
                    Notes                   = row.Notes,
                    Direction               = direction,
                    Source                  = "IMPORT",
                    SourceTransactionsFileId = fileId,
                    ExternalId              = row.Hash,
                    TenantId                = importFile.TenantId,
                    TransferGroupId         = transferGroupId
                };

                newTransactions.Add(txn);
                
                if (isInternal && counterAccountId.HasValue)
                {
                    var counterTxn = new Transaction
                    {
                        EntityId                 = entityId,
                        TxnTime                 = DateTime.SpecifyKind(row.TransactionDate, DateTimeKind.Utc),
                        Description             = row.Description,
                        TxnType                 = txnType,
                        AccountId               = counterAccountId.Value,
                        Amount                  = row.Amount,
                        Currency                = importFile.Account?.Currency ?? "AUD",
                        Direction               = direction == TransactionDirection.Inflow ? TransactionDirection.Outflow : TransactionDirection.Inflow,
                        Source                  = "IMPORT_COUNTER",
                        SourceTransactionsFileId = fileId,
                        TenantId                = importFile.TenantId,
                        TransferGroupId         = transferGroupId
                    };
                    newTransactions.Add(counterTxn);
                }
            }

            _db.Transactions.AddRange(newTransactions);

            importFile.Status = ImportStatus.Confirmed;
            importFile.ProcessedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            var accountsToRecalculate = newTransactions
                .Select(t => t.AccountId)
                .Distinct();

            foreach (var accId in accountsToRecalculate)
            {
                await _accountService.RecalculateBalanceAsync(accId, entityId);
            }

            return MapFileToDto(importFile, importFile.Account?.Name);
        }
        catch
        {
            await dbTransaction.RollbackAsync();
            importFile.Status = ImportStatus.Failed;
            await _db.SaveChangesAsync();
            throw;
        }
    }

    public async Task<List<ImportedTransactionsFileDto>> GetImportFilesAsync(Guid entityId)
    {
        return await _db.ImportedTransactionsFiles
            .Include(f => f.Account)
            .Where(f => f.EntityId == entityId)
            .OrderByDescending(f => f.UploadedAt)
            .Select(f => MapFileToDto(f, f.Account != null ? f.Account.Name : null))
            .ToListAsync();
    }

    public async Task DeleteImportFileAsync(Guid fileId, Guid entityId)
    {
        var importFile = await _db.ImportedTransactionsFiles
            .Include(f => f.StagingRows)
            .FirstOrDefaultAsync(f => f.Id == fileId && f.EntityId == entityId)
            ?? throw new KeyNotFoundException("Import file not found or access denied.");

        importFile.IsDeleted = true;
        importFile.UpdatedAt = DateTime.UtcNow;

        foreach (var row in importFile.StagingRows)
        {
            row.IsDeleted = true;
            row.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
    }

    private List<ImportedTransactionsFileStaging> ParseQif(
        Stream stream,
        ImportedTransactionsFile importFile,
        string tenantId,
        string? dateFormat)
    {
        var rows = new List<ImportedTransactionsFileStaging>();

        string? rawDate = null;
        string? rawAmount = null;
        string? description = null;
        string? notes = null;

        using var reader = new StreamReader(stream, leaveOpen: true);
        string? line;

        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            char code = line[0];
            string value = line.Length > 1 ? line[1..].Trim() : string.Empty;

            switch (code)
            {
                case '!': 
                    break;
                case 'D':
                    rawDate = value;
                    break;
                case 'T':
                    rawAmount = value.Replace(",", "");
                    break;
                case 'P':
                    description = value;
                    break;
                case 'M':
                    notes = value;
                    break;
                case '^': 
                    rows.Add(FinalizeRow(importFile, tenantId, rawDate, rawAmount, description, notes, dateFormat));
                    rawDate = null;
                    rawAmount = null;
                    description = null;
                    notes = null;
                    break;
            }
        }

        if (rawDate != null || rawAmount != null || description != null)
        {
            rows.Add(FinalizeRow(importFile, tenantId, rawDate, rawAmount, description, notes, dateFormat,
                errorReason: "Missing transaction terminator (^)"));
        }

        return rows;
    }

    private ImportedTransactionsFileStaging FinalizeRow(
        ImportedTransactionsFile importFile,
        string tenantId,
        string? rawDate,
        string? rawAmount,
        string? description,
        string? notes,
        string? dateFormat = null,
        string? errorReason = null)
    {
        var row = new ImportedTransactionsFileStaging
        {
            ImportedTransactionsFileId = importFile.Id,
            EntityId = importFile.EntityId,
            AccountId = importFile.AccountId,
            TenantId = tenantId,
            Description = description?.Trim() ?? string.Empty,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        };

        if (string.IsNullOrWhiteSpace(rawDate))
        {
            row.TransactionDate = DateTime.UtcNow; 
            row.Status = StagingStatus.Error;
            row.ErrorReason = "Missing date";
            return row;
        }

        if (!TryParseQifDate(rawDate, out var parsedDate, dateFormat))
        {
            row.TransactionDate = DateTime.UtcNow; 
            row.Status = StagingStatus.Error;
            row.ErrorReason = $"Invalid date format: '{rawDate}'";
            return row;
        }

        row.TransactionDate = parsedDate;

        if (string.IsNullOrWhiteSpace(rawAmount))
        {
            row.Status = StagingStatus.Error;
            row.ErrorReason = "Missing amount";
            return row;
        }

        if (!decimal.TryParse(rawAmount, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var amountRaw))
        {
            row.Status = StagingStatus.Error;
            row.ErrorReason = $"Non-numeric amount: '{rawAmount}'";
            return row;
        }

        if (amountRaw == 0)
        {
            row.Status = StagingStatus.Error;
            row.ErrorReason = "Zero-value transaction ignored";
            return row;
        }

        row.AmountRaw = amountRaw;
        row.Amount = Math.Abs(amountRaw);

        if (errorReason != null)
        {
            row.Status = StagingStatus.Error;
            row.ErrorReason = errorReason;
            return row;
        }

        row.Hash = ComputeHash(
            tenantId,
            importFile.EntityId,
            importFile.AccountId,
            parsedDate,
            amountRaw,
            row.Description);

        row.Status = StagingStatus.Valid;
        return row;
    }

    private static bool TryParseQifDate(string raw, out DateTime date, string? dateFormat = null)
    {
        var defaultFormats = new List<string>
        {
            "MM/dd/yyyy", "M/d/yyyy", "MM/dd/yy", "M/d/yy",
            "dd/MM/yyyy", "d/M/yyyy",
            "yyyy-MM-dd",
            "MM-dd-yyyy", "M-d-yyyy"
        };

        var formatsToTry = new List<string>();

        if (dateFormat == "DD/MM/YYYY")
        {
            formatsToTry.AddRange(["dd/MM/yyyy", "d/M/yyyy", "dd/MM/yy", "d/M/yy"]);
        }
        else if (dateFormat == "MM/DD/YYYY")
        {
            formatsToTry.AddRange(["MM/dd/yyyy", "M/d/yyyy", "MM/dd/yy", "M/d/yy"]);
        }
        else if (dateFormat == "YYYY-MM-DD")
        {
            formatsToTry.AddRange(["yyyy-MM-dd"]);
        }
        
        formatsToTry.AddRange(defaultFormats);

        if (DateTime.TryParseExact(raw, [.. formatsToTry],
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out var parsed))
        {
            date = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            return true;
        }

        date = default;
        return false;
    }

    private static string ComputeHash(
        string tenantId,
        Guid entityId,
        Guid accountId,
        DateTime date,
        decimal amountRaw,
        string description)
    {
        var normalized = NormalizeText(description);
        var input = $"{tenantId}|{entityId}|{accountId}|{date:yyyy-MM-dd}|{amountRaw}|{normalized}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string NormalizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        return Regex.Replace(text.Trim().ToLowerInvariant(), @"\s+", " ");
    }

    private static TransactionType ResolveInternalTxnType(
        Dictionary<Guid, Account> accounts,
        Guid primaryAccountId,
        Guid counterAccountId,
        TransactionDirection direction)
    {
        accounts.TryGetValue(primaryAccountId, out var primary);
        accounts.TryGetValue(counterAccountId, out var counter);

        if (primary == null || counter == null)
            return TransactionType.Transfer;

        var pt = primary.AccountType;
        var ct = counter.AccountType;

        if (pt == AccountType.ASSET && ct == AccountType.ASSET)
            return TransactionType.Transfer;

        if (pt == AccountType.ASSET && ct == AccountType.LIABILITY)
            return direction == TransactionDirection.Outflow
                ? TransactionType.LoanRepayment
                : TransactionType.LoanDisbursement;

        if (pt == AccountType.LIABILITY && ct == AccountType.ASSET)
            return direction == TransactionDirection.Inflow
                ? TransactionType.LoanRepayment
                : TransactionType.LoanDisbursement;

        return TransactionType.Transfer;
    }

    private static string ComputeInferredType(ImportedTransactionsFileStaging s)
    {
        bool isInternal = s.CounterAccountId.HasValue;
        if (!isInternal)
            return s.AmountRaw > 0 ? "Income" : "Expense";

        var direction = s.AmountRaw > 0 ? TransactionDirection.Inflow : TransactionDirection.Outflow;
        var primary = s.PrimaryAccount;
        var counter = s.CounterAccount;

        if (primary == null || counter == null)
            return s.AmountRaw > 0 ? "Income" : "Expense";

        var pt = primary.AccountType;
        var ct = counter.AccountType;

        if (pt == AccountType.ASSET && ct == AccountType.ASSET)
            return "Transfer";

        if (pt == AccountType.ASSET && ct == AccountType.LIABILITY)
            return direction == TransactionDirection.Outflow ? "Loan Repayment" : "Loan Disbursement";

        if (pt == AccountType.LIABILITY && ct == AccountType.ASSET)
            return direction == TransactionDirection.Inflow ? "Loan Repayment" : "Loan Disbursement";

        return "Transfer";
    }

    private static ImportedTransactionsFileDto MapFileToDto(
        ImportedTransactionsFile f, string? accountName) => new()
    {
        Id = f.Id,
        EntityId = f.EntityId,
        AccountId = f.AccountId,
        AccountName = accountName,
        OriginalFilename = f.OriginalFilename,
        UploadedAt = f.UploadedAt,
        ProcessedAt = f.ProcessedAt,
        Status = f.Status.ToString(),
        AllowDuplicates = f.AllowDuplicates,
        TotalRows = f.TotalRows,
        ValidRows = f.ValidRows,
        DuplicateRows = f.DuplicateRows,
        AlreadyImportedRows = f.AlreadyImportedRows,
        ErrorRows = f.ErrorRows
    };

    private static StagingRowDto MapRowToDto(ImportedTransactionsFileStaging s, string inferredType) => new()
    {
        Id = s.Id,
        ImportedTransactionsFileId = s.ImportedTransactionsFileId,
        AccountId = s.AccountId,
        CounterAccountId = s.CounterAccountId,
        CounterAccountName = s.CounterAccount?.Name,
        CategoryId = s.CategoryId,
        CategoryName = s.Category?.Name,
        TransactionDate = s.TransactionDate,
        Description = s.Description,
        Notes = s.Notes,
        Amount = s.Amount,
        AmountRaw = s.AmountRaw,
        Status = s.Status.ToString(),
        ErrorReason = s.ErrorReason,
        InferredType = inferredType // computed and passed explicitly to prevent loading issues in Select()
    };
}
