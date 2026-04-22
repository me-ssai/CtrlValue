using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CtrlValue.Domain;
using CtrlValue.Domain.Entities;
using CtrlValue.Domain.Enums;

namespace CtrlValue.Infrastructure.Data.Seeders;

/// <summary>
/// Runs once on startup and ensures the demo entity and all associated seed data
/// (accounts, categories, transactions, budgets, positions) exist in the database.
/// Idempotent: exits immediately if the demo entity is already present.
/// Skips entirely if Demo:Enabled = false in configuration.
/// </summary>
public class DemoDataSeeder : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<DemoDataSeeder> _logger;

    // ── Fixed deterministic IDs ──────────────────────────────────────────────
    // These are stable across deployments so the seeder is idempotent.

    // System role IDs for the demo entity
    private static readonly Guid DemoOwnerRoleId  = new("b1000000-0000-0000-0000-000000000001");
    private static readonly Guid DemoEditorRoleId = new("b1000000-0000-0000-0000-000000000002");
    private static readonly Guid DemoViewerRoleId = new("b1000000-0000-0000-0000-000000000003");

    // Account IDs
    private static readonly Guid EverydayCheckingId   = new("c1000000-0000-0000-0000-000000000001");
    private static readonly Guid HiSavingsId          = new("c1000000-0000-0000-0000-000000000002");
    private static readonly Guid EmergencyFundId      = new("c1000000-0000-0000-0000-000000000003");
    private static readonly Guid TermDepositId        = new("c1000000-0000-0000-0000-000000000004");
    private static readonly Guid CreditCardId         = new("c1000000-0000-0000-0000-000000000005");
    private static readonly Guid HecsDebtId           = new("c1000000-0000-0000-0000-000000000006");
    private static readonly Guid CarLoanId            = new("c1000000-0000-0000-0000-000000000007");
    private static readonly Guid EtfPortfolioId       = new("c1000000-0000-0000-0000-000000000008");
    private static readonly Guid CryptoWalletId       = new("c1000000-0000-0000-0000-000000000009");
    private static readonly Guid SuperannuationId     = new("c1000000-0000-0000-0000-000000000010");

    // Category IDs
    private static readonly Guid CatSalaryId          = new("d1000000-0000-0000-0000-000000000001");
    private static readonly Guid CatFreelanceId       = new("d1000000-0000-0000-0000-000000000002");
    private static readonly Guid CatInterestIncId     = new("d1000000-0000-0000-0000-000000000003");
    private static readonly Guid CatInvestIncId       = new("d1000000-0000-0000-0000-000000000004");
    private static readonly Guid CatRentId            = new("d1000000-0000-0000-0000-000000000005");
    private static readonly Guid CatGrocId            = new("d1000000-0000-0000-0000-000000000006");
    private static readonly Guid CatUtilId            = new("d1000000-0000-0000-0000-000000000007");
    private static readonly Guid CatTransportId       = new("d1000000-0000-0000-0000-000000000008");
    private static readonly Guid CatDiningId          = new("d1000000-0000-0000-0000-000000000009");
    private static readonly Guid CatEntertainId       = new("d1000000-0000-0000-0000-000000000010");
    private static readonly Guid CatHealthId          = new("d1000000-0000-0000-0000-000000000011");
    private static readonly Guid CatInsuranceId       = new("d1000000-0000-0000-0000-000000000012");
    private static readonly Guid CatSubsId            = new("d1000000-0000-0000-0000-000000000013");
    private static readonly Guid CatPhoneId           = new("d1000000-0000-0000-0000-000000000014");
    private static readonly Guid CatGymId             = new("d1000000-0000-0000-0000-000000000015");
    private static readonly Guid CatClothingId        = new("d1000000-0000-0000-0000-000000000016");
    private static readonly Guid CatTransferId        = new("d1000000-0000-0000-0000-000000000017");

    public DemoDataSeeder(IServiceProvider services, ILogger<DemoDataSeeder> logger)
    {
        _services = services;
        _logger   = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            if (await db.Entities.AnyAsync(e => e.Id == DemoConstants.DemoEntityId, cancellationToken))
            {
                _logger.LogInformation("DemoDataSeeder: demo entity already seeded, skipping.");
                return;
            }

            _logger.LogInformation("DemoDataSeeder: seeding demo entity and data...");

            var now = DateTime.UtcNow;

            await SeedEntityAndRolesAsync(db, now, cancellationToken);
            await SeedCategoriesAsync(db, now, cancellationToken);
            var accounts = await SeedAccountsAsync(db, now, cancellationToken);
            await SeedInstrumentsAndPositionsAsync(db, accounts, now, cancellationToken);
            await SeedTransactionsAsync(db, now, cancellationToken);
            await SeedBudgetsAsync(db, now, cancellationToken);

            _logger.LogInformation("DemoDataSeeder: demo entity seeded successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DemoDataSeeder: failed to seed demo data.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    // ── Seeding helpers ──────────────────────────────────────────────────────

    private static async Task SeedEntityAndRolesAsync(AppDbContext db, DateTime now, CancellationToken ct)
    {
        var entity = new Entity
        {
            Id           = DemoConstants.DemoEntityId,
            Name         = DemoConstants.DemoEntityName,
            BaseCurrency = "AUD",
            Country      = "AU",
            IsDemo       = true,
            TenantId     = DemoConstants.DemoTenantId,
            CreatedAt    = now,
        };
        db.Entities.Add(entity);

        db.EntityCustomRoles.AddRange(
            new EntityCustomRole { Id = DemoOwnerRoleId,  EntityId = DemoConstants.DemoEntityId, Name = "Owner",  IsSystem = true, TenantId = DemoConstants.DemoTenantId, CreatedAt = now },
            new EntityCustomRole { Id = DemoEditorRoleId, EntityId = DemoConstants.DemoEntityId, Name = "Editor", IsSystem = true, TenantId = DemoConstants.DemoTenantId, CreatedAt = now },
            new EntityCustomRole { Id = DemoViewerRoleId, EntityId = DemoConstants.DemoEntityId, Name = "Viewer", IsSystem = true, TenantId = DemoConstants.DemoTenantId, CreatedAt = now }
        );

        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedCategoriesAsync(AppDbContext db, DateTime now, CancellationToken ct)
    {
        var eid = DemoConstants.DemoEntityId;
        var tid = DemoConstants.DemoTenantId;

        db.Categories.AddRange(
            // Income
            new Category { Id = CatSalaryId,      EntityId = eid, TenantId = tid, Name = "Salary",           CategoryType = CategoryType.INCOME,    Color = "#22c55e", CreatedAt = now },
            new Category { Id = CatFreelanceId,   EntityId = eid, TenantId = tid, Name = "Freelance Income", CategoryType = CategoryType.INCOME,    Color = "#16a34a", CreatedAt = now },
            new Category { Id = CatInterestIncId, EntityId = eid, TenantId = tid, Name = "Interest Income",  CategoryType = CategoryType.INCOME,    Color = "#4ade80", CreatedAt = now },
            new Category { Id = CatInvestIncId,   EntityId = eid, TenantId = tid, Name = "Investment Income",CategoryType = CategoryType.INCOME,    Color = "#86efac", CreatedAt = now },
            // Expense
            new Category { Id = CatRentId,        EntityId = eid, TenantId = tid, Name = "Rent / Mortgage",  CategoryType = CategoryType.EXPENSE,   Color = "#ef4444", CreatedAt = now },
            new Category { Id = CatGrocId,        EntityId = eid, TenantId = tid, Name = "Groceries",        CategoryType = CategoryType.EXPENSE,   Color = "#f97316", CreatedAt = now },
            new Category { Id = CatUtilId,        EntityId = eid, TenantId = tid, Name = "Utilities",        CategoryType = CategoryType.EXPENSE,   Color = "#eab308", CreatedAt = now },
            new Category { Id = CatTransportId,   EntityId = eid, TenantId = tid, Name = "Transport",        CategoryType = CategoryType.EXPENSE,   Color = "#3b82f6", CreatedAt = now },
            new Category { Id = CatDiningId,      EntityId = eid, TenantId = tid, Name = "Dining Out",       CategoryType = CategoryType.EXPENSE,   Color = "#f43f5e", CreatedAt = now },
            new Category { Id = CatEntertainId,   EntityId = eid, TenantId = tid, Name = "Entertainment",    CategoryType = CategoryType.EXPENSE,   Color = "#8b5cf6", CreatedAt = now },
            new Category { Id = CatHealthId,      EntityId = eid, TenantId = tid, Name = "Health",           CategoryType = CategoryType.EXPENSE,   Color = "#06b6d4", CreatedAt = now },
            new Category { Id = CatInsuranceId,   EntityId = eid, TenantId = tid, Name = "Insurance",        CategoryType = CategoryType.EXPENSE,   Color = "#64748b", CreatedAt = now },
            new Category { Id = CatSubsId,        EntityId = eid, TenantId = tid, Name = "Subscriptions",    CategoryType = CategoryType.EXPENSE,   Color = "#ec4899", CreatedAt = now },
            new Category { Id = CatPhoneId,       EntityId = eid, TenantId = tid, Name = "Phone",            CategoryType = CategoryType.EXPENSE,   Color = "#6366f1", CreatedAt = now },
            new Category { Id = CatGymId,         EntityId = eid, TenantId = tid, Name = "Gym & Fitness",    CategoryType = CategoryType.EXPENSE,   Color = "#14b8a6", CreatedAt = now },
            new Category { Id = CatClothingId,    EntityId = eid, TenantId = tid, Name = "Clothing",         CategoryType = CategoryType.EXPENSE,   Color = "#a855f7", CreatedAt = now },
            // Transfer
            new Category { Id = CatTransferId,    EntityId = eid, TenantId = tid, Name = "Internal Transfer",CategoryType = CategoryType.TRANSFER,  Color = "#94a3b8", CreatedAt = now }
        );

        await db.SaveChangesAsync(ct);
    }

    private static async Task<Dictionary<Guid, Account>> SeedAccountsAsync(AppDbContext db, DateTime now, CancellationToken ct)
    {
        var eid = DemoConstants.DemoEntityId;
        var tid = DemoConstants.DemoTenantId;
        var eightMonthsAgo = now.AddMonths(-18);

        var accounts = new[]
        {
            new Account
            {
                Id = EverydayCheckingId, EntityId = eid, TenantId = tid,
                Name = "CommBank Everyday", AccountType = AccountType.ASSET,
                AssetClass = AssetClass.CASH, LiquidityClass = LiquidityClass.LIQUID,
                Currency = "AUD", Institution = "Commonwealth Bank",
                CurrentBalance = 4218.55m, StartingBalance = 4218.55m, StartingBalanceDate = eightMonthsAgo,
                IsActive = true, IsSyncEnabled = false, CreatedAt = now,
            },
            new Account
            {
                Id = HiSavingsId, EntityId = eid, TenantId = tid,
                Name = "ING Savings Maximiser", AccountType = AccountType.ASSET,
                AssetClass = AssetClass.CASH, LiquidityClass = LiquidityClass.LIQUID,
                Currency = "AUD", Institution = "ING",
                CurrentBalance = 18450.00m, StartingBalance = 18450.00m, StartingBalanceDate = eightMonthsAgo,
                IsActive = true, IsSyncEnabled = false, CreatedAt = now,
            },
            new Account
            {
                Id = EmergencyFundId, EntityId = eid, TenantId = tid,
                Name = "Macquarie Emergency Fund", AccountType = AccountType.ASSET,
                AssetClass = AssetClass.CASH, LiquidityClass = LiquidityClass.SEMI_LIQUID,
                Currency = "AUD", Institution = "Macquarie Bank",
                CurrentBalance = 10000.00m, StartingBalance = 10000.00m, StartingBalanceDate = eightMonthsAgo,
                IsActive = true, IsSyncEnabled = false, CreatedAt = now,
            },
            new Account
            {
                Id = TermDepositId, EntityId = eid, TenantId = tid,
                Name = "NAB Term Deposit", AccountType = AccountType.ASSET,
                AssetClass = AssetClass.CASH, LiquidityClass = LiquidityClass.LOCKED,
                Currency = "AUD", Institution = "National Australia Bank",
                CurrentBalance = 5000.00m, StartingBalance = 5000.00m, StartingBalanceDate = eightMonthsAgo,
                IsActive = true, IsSyncEnabled = false, CreatedAt = now,
            },
            new Account
            {
                Id = CreditCardId, EntityId = eid, TenantId = tid,
                Name = "ANZ Platinum Card", AccountType = AccountType.LIABILITY,
                AssetClass = AssetClass.CASH, LiquidityClass = LiquidityClass.LIQUID,
                Currency = "AUD", Institution = "ANZ", CreditLimit = 6000m,
                CurrentBalance = -1847.30m, StartingBalance = -1847.30m, StartingBalanceDate = eightMonthsAgo,
                IsActive = true, IsSyncEnabled = false, CreatedAt = now,
            },
            new Account
            {
                Id = HecsDebtId, EntityId = eid, TenantId = tid,
                Name = "ATO HECS-HELP Debt", AccountType = AccountType.LIABILITY,
                AssetClass = AssetClass.OTHER, LiquidityClass = LiquidityClass.ILLIQUID,
                Currency = "AUD", Institution = "Australian Tax Office",
                CurrentBalance = -21840.00m, StartingBalance = -21840.00m, StartingBalanceDate = eightMonthsAgo,
                IsActive = true, IsSyncEnabled = false, CreatedAt = now,
            },
            new Account
            {
                Id = CarLoanId, EntityId = eid, TenantId = tid,
                Name = "ANZ Car Loan", AccountType = AccountType.LIABILITY,
                AssetClass = AssetClass.VEHICLE, LiquidityClass = LiquidityClass.ILLIQUID,
                Currency = "AUD", Institution = "ANZ",
                CurrentBalance = -13940.00m, StartingBalance = -13940.00m, StartingBalanceDate = eightMonthsAgo,
                IsActive = true, IsSyncEnabled = false, CreatedAt = now,
            },
            new Account
            {
                Id = EtfPortfolioId, EntityId = eid, TenantId = tid,
                Name = "SelfWealth ETF Portfolio", AccountType = AccountType.ASSET,
                AssetClass = AssetClass.ETF, LiquidityClass = LiquidityClass.SEMI_LIQUID,
                Currency = "AUD", Institution = "SelfWealth",
                CurrentBalance = 31250.00m, StartingBalance = 31250.00m, StartingBalanceDate = eightMonthsAgo,
                IsActive = true, IsSyncEnabled = false, CreatedAt = now,
            },
            new Account
            {
                Id = CryptoWalletId, EntityId = eid, TenantId = tid,
                Name = "Self-Custody Crypto Wallet", AccountType = AccountType.ASSET,
                AssetClass = AssetClass.CRYPTO, LiquidityClass = LiquidityClass.SEMI_LIQUID,
                Currency = "AUD", Institution = "Self-Custody",
                CurrentBalance = 8720.00m, StartingBalance = 8720.00m, StartingBalanceDate = eightMonthsAgo,
                IsActive = true, IsSyncEnabled = false, CreatedAt = now,
            },
            new Account
            {
                Id = SuperannuationId, EntityId = eid, TenantId = tid,
                Name = "AustralianSuper", AccountType = AccountType.ASSET,
                AssetClass = AssetClass.SUPER, LiquidityClass = LiquidityClass.LOCKED,
                Currency = "AUD", Institution = "AustralianSuper",
                CurrentBalance = 42100.00m, StartingBalance = 42100.00m, StartingBalanceDate = eightMonthsAgo,
                IsActive = true, IsSyncEnabled = false, CreatedAt = now,
            },
        };

        db.Accounts.AddRange(accounts);
        await db.SaveChangesAsync(ct);

        return accounts.ToDictionary(a => a.Id);
    }

    private static async Task SeedInstrumentsAndPositionsAsync(
        AppDbContext db,
        Dictionary<Guid, Account> accounts,
        DateTime now,
        CancellationToken ct)
    {
        // Ensure VAS, VGS, NDQ instruments exist (not marked IsDefault — they are AU ETFs)
        var demoSymbols = new[] { "VAS", "VGS", "NDQ" };
        var existing = await db.Instruments
            .Where(i => demoSymbols.Contains(i.Symbol) && !i.IsDeleted)
            .Select(i => new { i.Id, i.Symbol })
            .ToListAsync(ct);
        var existingMap = existing.ToDictionary(x => x.Symbol, x => x.Id);

        Guid GetOrCreateInstrumentId(string symbol, string name, InstrumentType type, string ext = "")
        {
            if (existingMap.TryGetValue(symbol, out var id)) return id;
            var inst = new Instrument
            {
                Symbol = symbol, Name = name, InstrumentType = type,
                PriceProvider = PriceProviderType.ALPHA_VANTAGE,
                Currency = "AUD", PriceUnit = MetalUnit.UNIT,
                Exchange = "ASX", IsDefault = false,
                TenantId = DemoConstants.DemoTenantId, CreatedAt = now,
            };
            if (!string.IsNullOrEmpty(ext)) inst.ExternalSymbol = ext;
            db.Instruments.Add(inst);
            existingMap[symbol] = inst.Id;
            return inst.Id;
        }

        var vasId = GetOrCreateInstrumentId("VAS", "Vanguard Australian Shares ETF", InstrumentType.ETF);
        var vgsId = GetOrCreateInstrumentId("VGS", "Vanguard Intl Shares ETF (Hedged)", InstrumentType.ETF);
        var ndqId = GetOrCreateInstrumentId("NDQ", "Betashares NASDAQ 100 ETF", InstrumentType.ETF);

        // BTC already in instruments (IsDefault = true) — look it up
        var btcId = await db.Instruments
            .Where(i => i.Symbol == "BTC" && !i.IsDeleted)
            .Select(i => (Guid?)i.Id)
            .FirstOrDefaultAsync(ct);

        await db.SaveChangesAsync(ct); // save new instruments before positions

        var positions = new List<Position>
        {
            new() { AccountId = EtfPortfolioId, InstrumentId = vasId, Quantity = 85m,   Unit = MetalUnit.UNIT, CostBasisTotal = 9180m,  OpenedAt = now.AddMonths(-16), TenantId = DemoConstants.DemoTenantId, CreatedAt = now },
            new() { AccountId = EtfPortfolioId, InstrumentId = vgsId, Quantity = 62m,   Unit = MetalUnit.UNIT, CostBasisTotal = 11780m, OpenedAt = now.AddMonths(-14), TenantId = DemoConstants.DemoTenantId, CreatedAt = now },
            new() { AccountId = EtfPortfolioId, InstrumentId = ndqId, Quantity = 41m,   Unit = MetalUnit.UNIT, CostBasisTotal = 8610m,  OpenedAt = now.AddMonths(-10), TenantId = DemoConstants.DemoTenantId, CreatedAt = now },
        };

        if (btcId.HasValue)
            positions.Add(new Position { AccountId = CryptoWalletId, InstrumentId = btcId.Value, Quantity = 0.18m, Unit = MetalUnit.UNIT, CostBasisTotal = 6200m, OpenedAt = now.AddMonths(-12), TenantId = DemoConstants.DemoTenantId, CreatedAt = now });

        db.Positions.AddRange(positions);
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedTransactionsAsync(AppDbContext db, DateTime now, CancellationToken ct)
    {
        var eid = DemoConstants.DemoEntityId;
        var tid = DemoConstants.DemoTenantId;
        var txns = new List<Transaction>();

        // Helper to add a transaction
        void Add(DateTime date, string desc, decimal amount, TransactionType type,
                 TransactionDirection dir, Guid accountId, Guid? catId = null, string? merchant = null)
        {
            txns.Add(new Transaction
            {
                EntityId  = eid, TenantId = tid,
                TxnTime   = date, Description = desc, Amount = Math.Abs(amount),
                TxnType   = type, Direction = dir,
                AccountId = accountId, CategoryId = catId, Merchant = merchant,
                Currency  = "AUD", Source = "MANUAL", CreatedAt = now,
            });
        }

        // 18 months of data
        for (int m = 17; m >= 0; m--)
        {
            var d = now.AddMonths(-m);

            // ── Fortnightly salary (1st and 15th) ──
            var pay1 = new DateTime(d.Year, d.Month, 1, 9, 0, 0, DateTimeKind.Utc);
            var pay2 = new DateTime(d.Year, d.Month, 15, 9, 0, 0, DateTimeKind.Utc);
            Add(pay1, "Salary — TechCorp Pty Ltd", 4812m, TransactionType.Income, TransactionDirection.Inflow, EverydayCheckingId, CatSalaryId, "TechCorp Pty Ltd");
            Add(pay2, "Salary — TechCorp Pty Ltd", 4812m, TransactionType.Income, TransactionDirection.Inflow, EverydayCheckingId, CatSalaryId, "TechCorp Pty Ltd");

            // ── Freelance income (occasional, ~every 2 months) ──
            if (m % 2 == 0)
                Add(new DateTime(d.Year, d.Month, 20, 0, 0, 0, DateTimeKind.Utc), "Freelance — Web project", 1200m, TransactionType.Income, TransactionDirection.Inflow, EverydayCheckingId, CatFreelanceId);

            // ── Rent (1st of month) ──
            Add(new DateTime(d.Year, d.Month, 2, 10, 0, 0, DateTimeKind.Utc), "Rent — Realestate Agent", -1950m, TransactionType.Expense, TransactionDirection.Outflow, EverydayCheckingId, CatRentId, "Ray White");

            // ── Groceries (weekly, varying amounts) ──
            decimal[] grocAmts = { 127.40m, 93.85m, 151.20m, 88.60m };
            for (int w = 0; w < 4; w++)
            {
                var grocDay = new DateTime(d.Year, d.Month, Math.Min(7 * w + 5, 28), 11, 0, 0, DateTimeKind.Utc);
                Add(grocDay, "Woolworths Supermarket", -grocAmts[w], TransactionType.Expense, TransactionDirection.Outflow, EverydayCheckingId, CatGrocId, "Woolworths");
            }

            // ── Utilities (monthly) ──
            Add(new DateTime(d.Year, d.Month, 10, 0, 0, 0, DateTimeKind.Utc), "Origin Energy — Electricity & Gas", -185m, TransactionType.Expense, TransactionDirection.Outflow, EverydayCheckingId, CatUtilId, "Origin Energy");

            // ── Transport (weekly Opal / petrol) ──
            Add(new DateTime(d.Year, d.Month, 8,  0, 0, 0, DateTimeKind.Utc), "Opal Card Top-Up", -50m, TransactionType.Expense, TransactionDirection.Outflow, EverydayCheckingId, CatTransportId, "Transport NSW");
            Add(new DateTime(d.Year, d.Month, 17, 0, 0, 0, DateTimeKind.Utc), "Caltex Fuel",      -72m, TransactionType.Expense, TransactionDirection.Outflow, EverydayCheckingId, CatTransportId, "Caltex");

            // ── Dining out (2-3× per month) ──
            Add(new DateTime(d.Year, d.Month, 6,  19, 0, 0, DateTimeKind.Utc), "Chin Chin Restaurant",      -68m, TransactionType.Expense, TransactionDirection.Outflow, CreditCardId, CatDiningId, "Chin Chin");
            Add(new DateTime(d.Year, d.Month, 12, 12, 0, 0, DateTimeKind.Utc), "Thai Riff Kitchen",         -42m, TransactionType.Expense, TransactionDirection.Outflow, CreditCardId, CatDiningId, "Thai Riff");
            if (m % 3 != 0)
                Add(new DateTime(d.Year, d.Month, 24, 20, 0, 0, DateTimeKind.Utc), "Aria Restaurant",       -95m, TransactionType.Expense, TransactionDirection.Outflow, CreditCardId, CatDiningId, "Aria");

            // ── Entertainment / streaming ──
            Add(new DateTime(d.Year, d.Month, 3, 0, 0, 0, DateTimeKind.Utc), "Event Cinemas",    -28m, TransactionType.Expense, TransactionDirection.Outflow, CreditCardId, CatEntertainId, "Event Cinemas");
            if (m % 4 == 0)
                Add(new DateTime(d.Year, d.Month, 22, 0, 0, 0, DateTimeKind.Utc), "Live Nation — Concert Tix", -120m, TransactionType.Expense, TransactionDirection.Outflow, CreditCardId, CatEntertainId, "Live Nation");

            // ── Health ──
            if (m % 2 == 1)
                Add(new DateTime(d.Year, d.Month, 14, 9, 0, 0, DateTimeKind.Utc), "GP Bulk Billed Rebate", 38.20m, TransactionType.Income, TransactionDirection.Inflow, EverydayCheckingId, CatHealthId, "Medicare");
            if (m % 3 == 0)
                Add(new DateTime(d.Year, d.Month, 9, 0, 0, 0, DateTimeKind.Utc), "Priceline Pharmacy", -45m, TransactionType.Expense, TransactionDirection.Outflow, CreditCardId, CatHealthId, "Priceline");

            // ── Insurance (quarterly) ──
            if (m % 3 == 0)
                Add(new DateTime(d.Year, d.Month, 1, 8, 0, 0, DateTimeKind.Utc), "NRMA Car Insurance", -312m, TransactionType.Expense, TransactionDirection.Outflow, EverydayCheckingId, CatInsuranceId, "NRMA");

            // ── Subscriptions ──
            Add(new DateTime(d.Year, d.Month, 1, 0, 0, 0, DateTimeKind.Utc),  "Netflix",          -22.99m, TransactionType.Expense, TransactionDirection.Outflow, CreditCardId, CatSubsId, "Netflix");
            Add(new DateTime(d.Year, d.Month, 5, 0, 0, 0, DateTimeKind.Utc),  "Spotify Premium",  -11.99m, TransactionType.Expense, TransactionDirection.Outflow, CreditCardId, CatSubsId, "Spotify");
            Add(new DateTime(d.Year, d.Month, 5, 0, 0, 0, DateTimeKind.Utc),  "iCloud 200 GB",    -3.99m,  TransactionType.Expense, TransactionDirection.Outflow, CreditCardId, CatSubsId, "Apple");
            Add(new DateTime(d.Year, d.Month, 10, 0, 0, 0, DateTimeKind.Utc), "Microsoft 365",    -15.00m, TransactionType.Expense, TransactionDirection.Outflow, CreditCardId, CatSubsId, "Microsoft");

            // ── Phone ──
            Add(new DateTime(d.Year, d.Month, 20, 0, 0, 0, DateTimeKind.Utc), "Optus Mobile Plan", -55m, TransactionType.Expense, TransactionDirection.Outflow, EverydayCheckingId, CatPhoneId, "Optus");

            // ── Gym ──
            Add(new DateTime(d.Year, d.Month, 1, 0, 0, 0, DateTimeKind.Utc), "Fitness First Membership", -79m, TransactionType.Expense, TransactionDirection.Outflow, EverydayCheckingId, CatGymId, "Fitness First");

            // ── Clothing (occasional) ──
            if (m % 3 == 1)
                Add(new DateTime(d.Year, d.Month, 16, 0, 0, 0, DateTimeKind.Utc), "Country Road", -189m, TransactionType.Expense, TransactionDirection.Outflow, CreditCardId, CatClothingId, "Country Road");

            // ── Credit card payment (25th of month) ──
            Add(new DateTime(d.Year, d.Month, 25, 9, 0, 0, DateTimeKind.Utc), "Credit Card Payment — ANZ Platinum", -500m, TransactionType.Transfer, TransactionDirection.Outflow, EverydayCheckingId, CatTransferId);
            Add(new DateTime(d.Year, d.Month, 25, 9, 0, 0, DateTimeKind.Utc), "Credit Card Payment Received",        500m, TransactionType.Transfer, TransactionDirection.Inflow,  CreditCardId, CatTransferId);

            // ── Car loan repayment ──
            Add(new DateTime(d.Year, d.Month, 28, 9, 0, 0, DateTimeKind.Utc), "ANZ Car Loan Repayment", -580m, TransactionType.LoanRepayment, TransactionDirection.Outflow, EverydayCheckingId, null, "ANZ");

            // ── Transfer to savings (mid-month) ──
            Add(new DateTime(d.Year, d.Month, 16, 9, 0, 0, DateTimeKind.Utc), "Transfer to ING Savings", -1000m, TransactionType.Transfer, TransactionDirection.Outflow, EverydayCheckingId, CatTransferId);
            Add(new DateTime(d.Year, d.Month, 16, 9, 0, 0, DateTimeKind.Utc), "Transfer from CommBank",   1000m, TransactionType.Transfer, TransactionDirection.Inflow,  HiSavingsId, CatTransferId);

            // ── Interest on savings (monthly) ──
            Add(new DateTime(d.Year, d.Month, 28, 0, 0, 0, DateTimeKind.Utc), "ING Interest Credited", 64.50m, TransactionType.Income, TransactionDirection.Inflow, HiSavingsId, CatInterestIncId, "ING");

            // ── ETF buy (monthly, VAS + VGS alternating) ──
            if (m % 2 == 0)
                Add(new DateTime(d.Year, d.Month, 11, 10, 0, 0, DateTimeKind.Utc), "SelfWealth — Buy VAS x5", -540m, TransactionType.AssetPurchase, TransactionDirection.Outflow, EtfPortfolioId, null, "SelfWealth");
            else
                Add(new DateTime(d.Year, d.Month, 11, 10, 0, 0, DateTimeKind.Utc), "SelfWealth — Buy VGS x3", -570m, TransactionType.AssetPurchase, TransactionDirection.Outflow, EtfPortfolioId, null, "SelfWealth");

            // ── Superannuation contribution (monthly employer) ──
            Add(new DateTime(d.Year, d.Month, 15, 0, 0, 0, DateTimeKind.Utc), "Employer Super Contribution", 570m, TransactionType.CapitalDeposit, TransactionDirection.Inflow, SuperannuationId, null, "AustralianSuper");
        }

        // ── One-off / occasional transactions ──

        // BTC purchase (10 months ago)
        Add(now.AddMonths(-10).AddDays(3), "CoinSpot — Buy 0.08 BTC", -3800m, TransactionType.AssetPurchase, TransactionDirection.Outflow, CryptoWalletId, null, "CoinSpot");

        // BTC purchase (6 months ago)
        Add(now.AddMonths(-6).AddDays(7), "CoinSpot — Buy 0.05 BTC", -2400m, TransactionType.AssetPurchase, TransactionDirection.Outflow, CryptoWalletId, null, "CoinSpot");

        // NDQ buy
        Add(now.AddMonths(-9).AddDays(2), "SelfWealth — Buy NDQ x10", -1980m, TransactionType.AssetPurchase, TransactionDirection.Outflow, EtfPortfolioId, null, "SelfWealth");

        // Emergency fund top-up
        Add(now.AddMonths(-12), "Transfer to Emergency Fund", -2000m, TransactionType.Transfer, TransactionDirection.Outflow, EverydayCheckingId, CatTransferId);
        Add(now.AddMonths(-12), "Emergency Fund Top-Up",       2000m, TransactionType.Transfer, TransactionDirection.Inflow,  EmergencyFundId, CatTransferId);

        db.Transactions.AddRange(txns);
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedBudgetsAsync(AppDbContext db, DateTime now, CancellationToken ct)
    {
        var eid = DemoConstants.DemoEntityId;
        var tid = DemoConstants.DemoTenantId;
        var periodStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd   = periodStart.AddMonths(1).AddSeconds(-1);

        db.Budgets.AddRange(
            new Budget { EntityId = eid, TenantId = tid, CategoryId = CatGrocId,     PeriodType = BudgetPeriodType.MONTHLY, PeriodStart = periodStart, PeriodEnd = periodEnd, Amount = 600m,  Currency = "AUD", CreatedAt = now },
            new Budget { EntityId = eid, TenantId = tid, CategoryId = CatDiningId,   PeriodType = BudgetPeriodType.MONTHLY, PeriodStart = periodStart, PeriodEnd = periodEnd, Amount = 300m,  Currency = "AUD", CreatedAt = now },
            new Budget { EntityId = eid, TenantId = tid, CategoryId = CatTransportId,PeriodType = BudgetPeriodType.MONTHLY, PeriodStart = periodStart, PeriodEnd = periodEnd, Amount = 200m,  Currency = "AUD", CreatedAt = now },
            new Budget { EntityId = eid, TenantId = tid, CategoryId = CatEntertainId,PeriodType = BudgetPeriodType.MONTHLY, PeriodStart = periodStart, PeriodEnd = periodEnd, Amount = 150m,  Currency = "AUD", CreatedAt = now },
            new Budget { EntityId = eid, TenantId = tid, CategoryId = CatSubsId,     PeriodType = BudgetPeriodType.MONTHLY, PeriodStart = periodStart, PeriodEnd = periodEnd, Amount = 80m,   Currency = "AUD", CreatedAt = now },
            new Budget { EntityId = eid, TenantId = tid, CategoryId = CatGymId,      PeriodType = BudgetPeriodType.MONTHLY, PeriodStart = periodStart, PeriodEnd = periodEnd, Amount = 90m,   Currency = "AUD", CreatedAt = now }
        );

        await db.SaveChangesAsync(ct);
    }
}
