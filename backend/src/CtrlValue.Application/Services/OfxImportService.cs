using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain.Entities;
using CtrlValue.Domain.Enums;
using CtrlValue.Infrastructure.Data;

namespace CtrlValue.Application.Services;

public class OfxImportService : IOfxImportService
{
    private readonly AppDbContext _db;
    private readonly IAiCategorizationService _aiCategorization;
    private readonly IAccountService _accountService;
    private readonly ICategoryKeywordRuleService _keywordRuleService;
    private readonly IAccountKeywordRuleService _accountKeywordRuleService;

    public OfxImportService(AppDbContext db, IAiCategorizationService aiCategorization, IAccountService accountService, ICategoryKeywordRuleService keywordRuleService, IAccountKeywordRuleService accountKeywordRuleService)
    {
        _db = db;
        _aiCategorization = aiCategorization;
        _accountService = accountService;
        _keywordRuleService = keywordRuleService;
        _accountKeywordRuleService = accountKeywordRuleService;
    }

    public async Task<OfxImportedTransactionsFileDto> UploadAndStageAsync(
        Guid entityId,
        Guid accountId,
        bool allowDuplicates,
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

        string? importWarning = null;
        List<ImportedTransactionsFileStaging> parsedRows;
        try
        {
            parsedRows = ParseOfx(fileStream, importFile, account.TenantId, out importWarning);
        }
        catch (Exception ex)
        {
            importFile.Status = ImportStatus.Failed;
            await _db.SaveChangesAsync();
            var failDto = MapFileToDto(importFile, account.Name);
            failDto.ImportWarning = $"Failed to parse OFX file: {ex.Message}";
            return failDto;
        }

        if (parsedRows.Count == 0)
        {
            importFile.Status = ImportStatus.Failed;
            await _db.SaveChangesAsync();
            var failDto = MapFileToDto(importFile, account.Name);
            failDto.ImportWarning = importWarning ?? "No transactions found in OFX file.";
            return failDto;
        }

        var existingExternalIds = await _db.Transactions
            .Where(t => t.EntityId == entityId && !t.IsDeleted)
            .Select(t => t.ExternalId)
            .Where(h => h != null)
            .ToListAsync();
        var committedHashes = existingExternalIds.ToHashSet()!;

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
            
            // Initial account linking
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

        var dto = MapFileToDto(importFile, account.Name);
        dto.ImportWarning = importWarning;
        return dto;
    }

    public async Task<OfxStagedImportReviewDto> GetStagedImportAsync(Guid fileId, Guid entityId)
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

        return new OfxStagedImportReviewDto
        {
            File = MapFileToDto(importFile, importFile.Account?.Name),
            ValidRows = stagingRows.Where(s => s.Status == StagingStatus.Valid).Select(MapRowToDto).ToList(),
            DuplicateRows = stagingRows.Where(s => s.Status == StagingStatus.Duplicate).Select(MapRowToDto).ToList(),
            AlreadyImportedRows = stagingRows.Where(s => s.Status == StagingStatus.AlreadyImported).Select(MapRowToDto).ToList(),
            ErrorRows = stagingRows.Where(s => s.Status == StagingStatus.Error).Select(MapRowToDto).ToList()
        };
    }

    public async Task<OfxStagingRowDto> UpdateStagingRowAsync(
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
            var counterAccount = await _db.Accounts
                .FirstOrDefaultAsync(a => a.Id == request.CounterAccountId.Value && a.EntityId == entityId && a.IsActive);
            if (counterAccount == null)
                throw new ArgumentException("CounterAccount not found, access denied, or inactive.");
                
            if (counterAccount.Id == row.AccountId)
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

        return MapRowToDto(row);
    }

    public async Task<OfxImportedTransactionsFileDto> CommitImportAsync(Guid fileId, Guid entityId)
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
                    txnType = TransactionType.Income; // Or CapitalDeposit
                }
                else
                {
                    txnType = TransactionType.Expense; // Or CapitalWithdrawal
                }

                var txn = new Transaction
                {
                    EntityId               = entityId,
                    TxnTime               = DateTime.SpecifyKind(row.TransactionDate, DateTimeKind.Utc),
                    Description           = row.Description,
                    TxnType               = txnType,
                    AccountId             = accountId,
                    Amount                = row.Amount,
                    Currency              = row.Currency ?? importFile.Account?.Currency ?? "AUD",
                    CategoryId            = row.CategoryId,
                    Notes                 = row.Notes,
                    Direction             = direction,
                    Source                = "IMPORT",
                    SourceTransactionsFileId = fileId,
                    ExternalId            = row.Hash,
                    FitId                 = row.ExternalId,
                    TenantId              = importFile.TenantId,
                    TransferGroupId       = transferGroupId
                };

                newTransactions.Add(txn);
                
                if (isInternal && counterAccountId.HasValue)
                {
                    var counterTxn = new Transaction
                    {
                        EntityId               = entityId,
                        TxnTime               = DateTime.SpecifyKind(row.TransactionDate, DateTimeKind.Utc),
                        Description           = row.Description,
                        TxnType               = txnType,
                        AccountId             = counterAccountId.Value,
                        Amount                = row.Amount,
                        Currency              = row.Currency ?? importFile.Account?.Currency ?? "AUD",
                        Direction             = direction == TransactionDirection.Inflow ? TransactionDirection.Outflow : TransactionDirection.Inflow,
                        Source                = "IMPORT_COUNTER",
                        SourceTransactionsFileId = fileId,
                        TenantId              = importFile.TenantId,
                        TransferGroupId       = transferGroupId
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

    public async Task<List<OfxImportedTransactionsFileDto>> GetImportFilesAsync(Guid entityId)
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

    private List<ImportedTransactionsFileStaging> ParseOfx(
        Stream stream,
        ImportedTransactionsFile importFile,
        string tenantId,
        out string? importWarning)
    {
        importWarning = null;

        string raw;
        using (var reader = new StreamReader(stream, Encoding.Latin1, detectEncodingFromByteOrderMarks: true, leaveOpen: true))
            raw = reader.ReadToEnd();

        raw = raw.Replace("\r\n", "\n").Replace("\r", "\n");
        raw = Regex.Replace(raw, @"[\x00-\x08\x0b\x0c\x0e-\x1f]", string.Empty);

        bool isSgml = Regex.IsMatch(raw, @"OFXSGML", RegexOptions.IgnoreCase);
        XDocument doc;

        try
        {
            doc = isSgml ? NormalizeSgmlToXml(raw) : XDocument.Parse(raw);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"OFX file could not be parsed as {(isSgml ? "SGML" : "XML")}: {ex.Message}");
        }

        var ofxRoot = doc.Root?.Name.LocalName == "OFX"
            ? doc.Root
            : doc.Descendants("OFX").FirstOrDefault()
              ?? throw new InvalidOperationException("Could not locate <OFX> root element.");

        var stmtRsList = ofxRoot
            .Descendants("STMTRS")
            .Concat(ofxRoot.Descendants("CCSTMTRS"))
            .ToList();

        if (stmtRsList.Count == 0)
            throw new InvalidOperationException("No STMTRS or CCSTMTRS block found in OFX file.");

        if (stmtRsList.Count > 1)
            importWarning = $"Multiple statement blocks found in file — only the first was imported.";

        var stmtRs = stmtRsList[0];
        var currency = stmtRs.Element("CURDEF")?.Value?.Trim();

        var stmtTrnElements = stmtRs
            .Descendants("BANKTRANLIST")
            .FirstOrDefault()
            ?.Elements("STMTTRN")
            .ToList() ?? new List<XElement>();

        var rows = new List<ImportedTransactionsFileStaging>();
        foreach (var trn in stmtTrnElements)
            rows.Add(FinalizeRow(importFile, tenantId, trn, currency));

        return rows;
    }

    private static XDocument NormalizeSgmlToXml(string raw)
    {
        var ofxStart = raw.IndexOf("<OFX>", StringComparison.OrdinalIgnoreCase);
        if (ofxStart < 0)
            throw new InvalidOperationException("Cannot locate <OFX> opening tag.");

        var body = raw[ofxStart..];

        var closeTagPattern = new Regex(
            @"^(<([A-Z0-9.]+)>)([^<\r\n]+)$",
            RegexOptions.Multiline);

        body = closeTagPattern.Replace(body, m =>
        {
            var tag   = m.Groups[2].Value;
            var value = m.Groups[3].Value;
            return $"<{tag}>{value}</{tag}>";
        });

        var bodyAfterPass1 = body;
        body = Regex.Replace(body, @"^<([A-Z0-9.]+)>\s*$", m =>
        {
            var tag = m.Groups[1].Value;
            return bodyAfterPass1.Contains($"</{tag}>") ? m.Value : $"<{tag}></{tag}>";
        }, RegexOptions.Multiline);

        return XDocument.Parse(body);
    }

    private ImportedTransactionsFileStaging FinalizeRow(
        ImportedTransactionsFile importFile,
        string tenantId,
        XElement trn,
        string? currency)
    {
        var row = new ImportedTransactionsFileStaging
        {
            ImportedTransactionsFileId = importFile.Id,
            EntityId = importFile.EntityId,
            AccountId = importFile.AccountId,
            TenantId = tenantId,
            Currency = currency
        };

        var rawDate = trn.Element("DTPOSTED")?.Value?.Trim();
        if (string.IsNullOrWhiteSpace(rawDate) || rawDate.Length < 8)
        {
            row.TransactionDate = DateTime.UtcNow;
            row.Status = StagingStatus.Error;
            row.ErrorReason = string.IsNullOrWhiteSpace(rawDate)
                ? "Missing DTPOSTED"
                : $"DTPOSTED too short to parse: '{rawDate}'";
            return row;
        }

        var datePart = rawDate[..8];
        if (!DateTime.TryParseExact(datePart, "yyyyMMdd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var parsedDate))
        {
            row.TransactionDate = DateTime.UtcNow;
            row.Status = StagingStatus.Error;
            row.ErrorReason = $"Invalid DTPOSTED format: '{rawDate}'";
            return row;
        }

        row.TransactionDate = DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc);

        var rawAmount = trn.Element("TRNAMT")?.Value?.Trim();
        if (string.IsNullOrWhiteSpace(rawAmount))
        {
            row.Status = StagingStatus.Error;
            row.ErrorReason = "Missing TRNAMT";
            return row;
        }

        if (!decimal.TryParse(rawAmount,
                System.Globalization.NumberStyles.AllowLeadingSign |
                System.Globalization.NumberStyles.AllowDecimalPoint,
                System.Globalization.CultureInfo.InvariantCulture,
                out var amountRaw))
        {
            row.Status = StagingStatus.Error;
            row.ErrorReason = $"Non-numeric TRNAMT: '{rawAmount}'";
            return row;
        }

        if (amountRaw == 0)
        {
            row.Status = StagingStatus.Error;
            row.ErrorReason = "Zero-value transaction ignored.";
            return row;
        }

        row.AmountRaw = amountRaw;
        row.Amount = Math.Abs(amountRaw);

        var name  = trn.Element("NAME")?.Value?.Trim();
        var payee = trn.Element("PAYEE")?.Value?.Trim();
        var memo  = trn.Element("MEMO")?.Value?.Trim();

        row.Description = FirstNonEmpty(name, payee, memo) ?? "Unknown";

        if (!string.IsNullOrWhiteSpace(memo) && memo != row.Description)
            row.Notes = memo;

        var fitId = trn.Element("FITID")?.Value?.Trim();
        row.ExternalId = string.IsNullOrWhiteSpace(fitId) ? null : fitId;
        row.OfxTrnType = trn.Element("TRNTYPE")?.Value?.Trim();

        row.Hash = ComputeHash(tenantId, importFile.EntityId, importFile.AccountId,
            parsedDate, amountRaw, row.Description, row.ExternalId);

        row.Status = StagingStatus.Valid;
        return row;
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static string ComputeHash(
        string tenantId,
        Guid entityId,
        Guid accountId,
        DateTime date,
        decimal amountRaw,
        string description,
        string? fitId)
    {
        string input = fitId != null
            ? $"{tenantId}|{entityId}|{accountId}|FITID:{fitId}"
            : $"{tenantId}|{entityId}|{accountId}|{date:yyyy-MM-dd}|{amountRaw}|{NormalizeText(description)}";

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string NormalizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        return Regex.Replace(text.Trim().ToLowerInvariant(), @"\s+", " ");
    }

    private static OfxImportedTransactionsFileDto MapFileToDto(
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

    private static OfxStagingRowDto MapRowToDto(ImportedTransactionsFileStaging s) => new()
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
        InferredType = ComputeInferredType(s),
        ExternalId = s.ExternalId,
        Currency = s.Currency,
        OfxTrnType = s.OfxTrnType
    };

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
}
