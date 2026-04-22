using Microsoft.EntityFrameworkCore;
using CtrlValue.Domain.Entities;
using CtrlValue.Domain.Enums;

namespace CtrlValue.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Core tables
    public DbSet<User> Users => Set<User>();
    public DbSet<Entity> Entities => Set<Entity>();
    public DbSet<EntityUser> EntityUsers => Set<EntityUser>();
    public DbSet<EntityCustomRole> EntityCustomRoles => Set<EntityCustomRole>();
    public DbSet<EntityRolePermission> EntityRolePermissions => Set<EntityRolePermission>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Instrument> Instruments => Set<Instrument>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Valuation> Valuations => Set<Valuation>();
    
    // Supporting tables
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<CategoryKeywordRule> CategoryKeywordRules => Set<CategoryKeywordRule>();
    public DbSet<AccountKeywordRule> AccountKeywordRules => Set<AccountKeywordRule>();
    public DbSet<PriceHistory> PriceHistory => Set<PriceHistory>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<DepreciationSchedule> DepreciationSchedules => Set<DepreciationSchedule>();
    
    // Import tables
    public DbSet<ImportedTransactionsFile> ImportedTransactionsFiles => Set<ImportedTransactionsFile>();
    public DbSet<ImportedTransactionsFileStaging> ImportedTransactionsFilesStaging => Set<ImportedTransactionsFileStaging>();
    
    // Admin tables
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<UserDeletionRequest> DeletionRequests => Set<UserDeletionRequest>();

    // Audit tables
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // Loan tables
    public DbSet<LoanDetails> LoanDetails => Set<LoanDetails>();
    public DbSet<LoanRateHistory> LoanRateHistory => Set<LoanRateHistory>();

    // Investing extension tables
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<GlobalPriceCache> GlobalPriceCache => Set<GlobalPriceCache>();

    // Integration tables
    public DbSet<EntityIntegration> EntityIntegrations => Set<EntityIntegration>();
    public DbSet<PlatformIntegration> PlatformIntegrations => Set<PlatformIntegration>();
    public DbSet<EntityDefaultTicker> EntityDefaultTickers => Set<EntityDefaultTicker>();
    public DbSet<FinancialConnection> FinancialConnections => Set<FinancialConnection>();
    public DbSet<ConnectedAccount> ConnectedAccounts => Set<ConnectedAccount>();
    public DbSet<ConnectionSyncLog> ConnectionSyncLogs => Set<ConnectionSyncLog>();

    // Agent tables
    public DbSet<AgentSetting> AgentSettings => Set<AgentSetting>();
    public DbSet<AgentScenario> AgentScenarios => Set<AgentScenario>();
    public DbSet<AgentDigestEmail> AgentDigestEmails => Set<AgentDigestEmail>();
    public DbSet<AgentSavingsSnapshot> AgentSavingsSnapshots => Set<AgentSavingsSnapshot>();
    public DbSet<AgentFeatureFlag> AgentFeatureFlags => Set<AgentFeatureFlag>();
    public DbSet<AgentFeatureFlagAssignment> AgentFeatureFlagAssignments => Set<AgentFeatureFlagAssignment>();
    public DbSet<AgentConversation> AgentConversations => Set<AgentConversation>();
    public DbSet<AgentMessage> AgentMessages => Set<AgentMessage>();
    public DbSet<AgentContextSnapshot> AgentContextSnapshots => Set<AgentContextSnapshot>();
    public DbSet<AgentInsight> AgentInsights => Set<AgentInsight>();
    public DbSet<AgentAuditLog> AgentAuditLogs => Set<AgentAuditLog>();
    public DbSet<AgentWebResearchCache> AgentWebResearchCaches => Set<AgentWebResearchCache>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── User ──
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(u => u.Id);
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Email).HasMaxLength(256).IsRequired();
            e.Property(u => u.PasswordHash).IsRequired();
            e.Property(u => u.FirstName).HasMaxLength(100);
            e.Property(u => u.LastName).HasMaxLength(100);
            e.Property(u => u.TenantId).HasMaxLength(128);
            e.Property(u => u.Role).HasConversion<string>().HasMaxLength(20).HasDefaultValue(UserRole.User).HasSentinel(UserRole.SuperAdmin);
            e.Property(u => u.InviteToken).HasMaxLength(64);
            e.Property(u => u.InvitedEntityId);
            e.Property(u => u.FailedLoginAttempts).HasDefaultValue(0);
            e.HasQueryFilter(u => !u.IsDeleted);
        });

        // ── Entity ──
        modelBuilder.Entity<Entity>(e =>
        {
            e.ToTable("entity");
            e.HasKey(en => en.Id);
            e.Property(en => en.Name).HasMaxLength(256).IsRequired();
            e.Property(en => en.BaseCurrency).HasMaxLength(3).IsRequired();
            e.Property(en => en.Country).HasMaxLength(2).IsRequired().HasDefaultValue("AU");
            e.Property(en => en.IsDemo).HasDefaultValue(false);
            e.Property(en => en.TenantId).HasMaxLength(128);
            e.HasQueryFilter(en => !en.IsDeleted);
        });

        // ── EntityCustomRole ──
        modelBuilder.Entity<EntityCustomRole>(e =>
        {
            e.ToTable("entity_custom_roles");
            e.HasKey(r => r.Id);
            e.Property(r => r.Name).HasMaxLength(100).IsRequired();
            e.Property(r => r.TenantId).HasMaxLength(128);
            e.HasIndex(r => new { r.EntityId, r.Name }).IsUnique();

            e.HasOne(r => r.Entity)
                .WithMany(en => en.CustomRoles)
                .HasForeignKey(r => r.EntityId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasQueryFilter(r => !r.IsDeleted);
        });

        // ── EntityRolePermission ──
        modelBuilder.Entity<EntityRolePermission>(e =>
        {
            e.ToTable("entity_role_permissions");
            e.HasKey(p => new { p.RoleId, p.PermissionKey });
            e.Property(p => p.PermissionKey).HasMaxLength(100).IsRequired();

            e.HasOne(p => p.Role)
                .WithMany(r => r.RolePermissions)
                .HasForeignKey(p => p.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── EntityUser ──
        modelBuilder.Entity<EntityUser>(e =>
        {
            e.ToTable("entity_users");
            e.HasKey(eu => eu.Id);
            e.HasIndex(eu => new { eu.EntityId, eu.UserId }).IsUnique();
            e.Property(eu => eu.TenantId).HasMaxLength(128);

            e.HasOne(eu => eu.Entity)
                .WithMany(en => en.EntityUsers)
                .HasForeignKey(eu => eu.EntityId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(eu => eu.User)
                .WithMany(u => u.EntityUsers)
                .HasForeignKey(eu => eu.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(eu => eu.CustomRole)
                .WithMany(r => r.EntityUsers)
                .HasForeignKey(eu => eu.CustomRoleId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasQueryFilter(eu => !eu.IsDeleted);
        });

        // ── Account ──
        modelBuilder.Entity<Account>(e =>
        {
            e.ToTable("account");
            e.HasKey(a => a.Id);
            e.Property(a => a.Name).HasMaxLength(256).IsRequired();
            e.Property(a => a.Currency).HasMaxLength(3).IsRequired();
            e.Property(a => a.Institution).HasMaxLength(256);
            e.Property(a => a.AccountNumber).HasMaxLength(100);
            e.Property(a => a.ExternalId).HasMaxLength(256);
            e.Property(a => a.CreditLimit).HasPrecision(18, 2);
            e.Property(a => a.StartingBalance).HasPrecision(18, 2);
            e.Property(a => a.TenantId).HasMaxLength(128);
            e.Property(a => a.ConnectionProvider).HasConversion<string>().HasMaxLength(20);
            
            e.HasOne(a => a.Entity)
                .WithMany(en => en.Accounts)
                .HasForeignKey(a => a.EntityId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(a => a.LoanDetails)
                .WithOne(l => l.Account)
                .HasForeignKey<LoanDetails>(l => l.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasQueryFilter(a => !a.IsDeleted);
        });

        // ── Instrument ──
        modelBuilder.Entity<Instrument>(e =>
        {
            e.ToTable("instrument");
            e.HasKey(i => i.Id);
            e.HasIndex(i => i.Symbol).IsUnique();
            e.Property(i => i.Symbol).HasMaxLength(20).IsRequired();
            e.Property(i => i.Name).HasMaxLength(256).IsRequired();
            e.Property(i => i.Currency).HasMaxLength(3).IsRequired();
            e.Property(i => i.Exchange).HasMaxLength(100);
            e.Property(i => i.ExternalSymbol).HasMaxLength(100);
            e.Property(i => i.PriceProvider).HasConversion<string>().HasMaxLength(20);
            e.Property(i => i.PriceUnit).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(i => i.TenantId).HasMaxLength(128);
            // Bond fields
            e.Property(i => i.Issuer).HasMaxLength(200);
            e.Property(i => i.FaceValue).HasPrecision(18, 4);
            e.Property(i => i.CouponRate).HasPrecision(8, 4);
            e.Property(i => i.CouponFrequency).HasMaxLength(20);
            e.Property(i => i.CreditRating).HasMaxLength(10);
            // ETF / Fund fields
            e.Property(i => i.ExpenseRatio).HasPrecision(8, 4);
            e.Property(i => i.DistributionYield).HasPrecision(8, 4);
            e.Property(i => i.DistributionFrequency).HasMaxLength(20);
            e.Property(i => i.UnderlyingIndex).HasMaxLength(200);
            e.HasQueryFilter(i => !i.IsDeleted);
        });

        // ── Position ──
        modelBuilder.Entity<Position>(e =>
        {
            e.ToTable("position");
            e.HasKey(p => p.Id);
            e.Property(p => p.Quantity).HasPrecision(18, 6);
            e.Property(p => p.Unit).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(p => p.CostBasisTotal).HasPrecision(18, 2);
            e.Property(p => p.TenantId).HasMaxLength(128);
            
            e.HasOne(p => p.Account)
                .WithMany(a => a.Positions)
                .HasForeignKey(p => p.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
            
            e.HasOne(p => p.Instrument)
                .WithMany(i => i.Positions)
                .HasForeignKey(p => p.InstrumentId)
                .OnDelete(DeleteBehavior.SetNull);
            
            e.HasQueryFilter(p => !p.IsDeleted);
        });

        // ── Transaction ──
        modelBuilder.Entity<Transaction>(e =>
        {
            e.ToTable("txn");
            e.HasKey(t => t.Id);
            e.Property(t => t.Description).HasMaxLength(500);
            e.Property(t => t.Amount).HasPrecision(18, 2);
            e.Property(t => t.Currency).HasMaxLength(3).IsRequired();
            e.Property(t => t.Quantity).HasPrecision(18, 6);
            e.Property(t => t.UnitPrice).HasPrecision(18, 6);
            e.Property(t => t.Fees).HasPrecision(18, 2);
            e.Property(t => t.Merchant).HasMaxLength(256);
            e.Property(t => t.ExternalId).HasMaxLength(256);
            e.Property(t => t.ReceiptUrl).HasMaxLength(1000);
            e.Property(t => t.TenantId).HasMaxLength(128);
            e.Property(t => t.Notes).HasMaxLength(1000);
            e.Property(t => t.Source).HasMaxLength(20).HasDefaultValue("MANUAL");
            e.Property(t => t.Direction).HasConversion<string>().HasMaxLength(10);
            e.Property(t => t.FitId).HasMaxLength(256);
            
            e.HasOne(t => t.Entity)
                .WithMany(en => en.Transactions)
                .HasForeignKey(t => t.EntityId)
                .OnDelete(DeleteBehavior.Cascade);
            
            e.HasOne(t => t.Account)
                .WithMany(a => a.Transactions)
                .HasForeignKey(t => t.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
            
            e.HasOne(t => t.Instrument)
                .WithMany(i => i.Transactions)
                .HasForeignKey(t => t.InstrumentId)
                .OnDelete(DeleteBehavior.SetNull);
            
            e.HasOne(t => t.Category)
                .WithMany(c => c.Transactions)
                .HasForeignKey(t => t.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);
            
            e.HasQueryFilter(t => !t.IsDeleted);
        });

        // ── Valuation ──
        modelBuilder.Entity<Valuation>(e =>
        {
            e.ToTable("valuation");
            e.HasKey(v => v.Id);
            e.Property(v => v.Value).HasPrecision(18, 2);
            e.Property(v => v.Currency).HasMaxLength(3).IsRequired();
            e.Property(v => v.Source).HasMaxLength(100);
            e.Property(v => v.TenantId).HasMaxLength(128);
            
            e.HasOne(v => v.Account)
                .WithMany(a => a.Valuations)
                .HasForeignKey(v => v.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
            
            e.HasQueryFilter(v => !v.IsDeleted);
        });

        // ── Category ──
        modelBuilder.Entity<Category>(e =>
        {
            e.ToTable("category");
            e.HasKey(c => c.Id);
            e.Property(c => c.Name).HasMaxLength(200).IsRequired();
            e.Property(c => c.Color).HasMaxLength(7);
            e.Property(c => c.Icon).HasMaxLength(50);
            e.Property(c => c.TenantId).HasMaxLength(128);
            
            e.HasOne(c => c.Entity)
                .WithMany(en => en.Categories)
                .HasForeignKey(c => c.EntityId)
                .OnDelete(DeleteBehavior.Cascade);
            
            e.HasOne(c => c.ParentCategory)
                .WithMany(c => c.SubCategories)
                .HasForeignKey(c => c.ParentCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            
            e.HasQueryFilter(c => !c.IsDeleted);
        });

        // ── PriceHistory ──
        modelBuilder.Entity<PriceHistory>(e =>
        {
            e.ToTable("price_history");
            e.HasKey(ph => ph.Id);
            e.HasIndex(ph => new { ph.InstrumentId, ph.AsOfDate }).IsUnique();
            e.Property(ph => ph.OpenPrice).HasPrecision(18, 8);
            e.Property(ph => ph.ClosePrice).HasPrecision(18, 8);
            e.Property(ph => ph.HighPrice).HasPrecision(18, 8);
            e.Property(ph => ph.LowPrice).HasPrecision(18, 8);
            e.Property(ph => ph.Currency).HasMaxLength(3).IsRequired();
            e.Property(ph => ph.Source).HasMaxLength(50);
            e.Property(ph => ph.TenantId).HasMaxLength(128);
            
            e.HasOne(ph => ph.Instrument)
                .WithMany(i => i.PriceHistory)
                .HasForeignKey(ph => ph.InstrumentId)
                .OnDelete(DeleteBehavior.Cascade);
            
            e.HasQueryFilter(ph => !ph.IsDeleted);
        });

        // ── Budget ──
        modelBuilder.Entity<Budget>(e =>
        {
            e.ToTable("budget");
            e.HasKey(b => b.Id);
            e.Property(b => b.Amount).HasPrecision(18, 2);
            e.Property(b => b.Currency).HasMaxLength(3).IsRequired();
            e.Property(b => b.TenantId).HasMaxLength(128);
            
            e.HasOne(b => b.Entity)
                .WithMany(en => en.Budgets)
                .HasForeignKey(b => b.EntityId)
                .OnDelete(DeleteBehavior.Cascade);
            
            e.HasOne(b => b.Category)
                .WithMany(c => c.Budgets)
                .HasForeignKey(b => b.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
            
            e.HasQueryFilter(b => !b.IsDeleted);
        });

        // ── DepreciationSchedule ──
        modelBuilder.Entity<DepreciationSchedule>(e =>
        {
            e.ToTable("depreciation_schedule");
            e.HasKey(ds => ds.Id);
            e.Property(ds => ds.PurchasePrice).HasPrecision(18, 2);
            e.Property(ds => ds.SalvageValue).HasPrecision(18, 2);
            e.Property(ds => ds.AnnualDepreciationRate).HasPrecision(5, 2);
            e.Property(ds => ds.TenantId).HasMaxLength(128);
            
            e.HasOne(ds => ds.Account)
                .WithOne(a => a.DepreciationSchedule)
                .HasForeignKey<DepreciationSchedule>(ds => ds.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
            
            e.HasQueryFilter(ds => !ds.IsDeleted);
        });

        // ── ImportedTransactionsFile ──
        modelBuilder.Entity<ImportedTransactionsFile>(e =>
        {
            e.ToTable("imported_transactions_files");
            e.HasKey(f => f.Id);
            e.Property(f => f.OriginalFilename).HasMaxLength(500).IsRequired();
            e.Property(f => f.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(f => f.TenantId).HasMaxLength(128);

            e.HasIndex(f => f.TenantId);
            e.HasIndex(f => f.EntityId);
            e.HasIndex(f => f.AccountId);

            e.HasOne(f => f.Entity)
                .WithMany()
                .HasForeignKey(f => f.EntityId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(f => f.Account)
                .WithMany()
                .HasForeignKey(f => f.AccountId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasQueryFilter(f => !f.IsDeleted);
        });

        // ── ImportedTransactionsFileStaging ──
        modelBuilder.Entity<ImportedTransactionsFileStaging>(e =>
        {
            e.ToTable("imported_transactions_files_staging");
            e.HasKey(s => s.Id);
            e.Property(s => s.Description).HasMaxLength(500).IsRequired();
            e.Property(s => s.Notes).HasMaxLength(1000);
            e.Property(s => s.Amount).HasPrecision(18, 4);
            e.Property(s => s.AmountRaw).HasPrecision(18, 4);
            e.Property(s => s.Hash).HasMaxLength(64).IsRequired();
            e.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(s => s.ErrorReason).HasMaxLength(500);
            e.Property(s => s.TenantId).HasMaxLength(128);
            
            // OFX-specific columns
            e.Property(s => s.ExternalId).HasMaxLength(256);
            e.Property(s => s.Currency).HasMaxLength(10);
            e.Property(s => s.OfxTrnType).HasMaxLength(20);

            // Unique index for hash-based duplicate detection (QIF + OFX fallback)
            e.HasIndex(s => new { s.TenantId, s.EntityId, s.AccountId, s.ImportedTransactionsFileId, s.Hash });

            // Partial unique index for FITID-based duplicate detection (OFX primary)
            e.HasIndex(s => new { s.TenantId, s.EntityId, s.AccountId, s.ExternalId })
             .HasFilter("\"ExternalId\" IS NOT NULL")
             .HasDatabaseName("IX_staging_fitid");

            e.HasIndex(s => s.TransactionDate);
            e.HasIndex(s => s.Status);
            e.HasIndex(s => s.EntityId);

            e.HasOne(s => s.ImportFile)
                .WithMany(f => f.StagingRows)
                .HasForeignKey(s => s.ImportedTransactionsFileId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(s => s.PrimaryAccount)
                .WithMany()
                .HasForeignKey(s => s.AccountId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(s => s.CounterAccount)
                .WithMany()
                .HasForeignKey(s => s.CounterAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(s => s.Category)
                .WithMany()
                .HasForeignKey(s => s.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasQueryFilter(s => !s.IsDeleted);
        });
        // ── CategoryKeywordRule ──
        modelBuilder.Entity<CategoryKeywordRule>(e =>
        {
            e.ToTable("category_keyword_rule");
            e.HasKey(r => r.Id);
            e.Property(r => r.Keyword).HasMaxLength(256).IsRequired();
            e.Property(r => r.NormalizedKeyword).HasMaxLength(256).IsRequired();
            e.Property(r => r.MatchType).HasConversion<string>().HasMaxLength(20).HasDefaultValue(KeywordMatchType.Contains);
            e.Property(r => r.TenantId).HasMaxLength(128);

            e.HasIndex(r => new { r.EntityId, r.NormalizedKeyword }).IsUnique();

            e.HasOne(r => r.Entity)
                .WithMany()
                .HasForeignKey(r => r.EntityId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(r => r.Category)
                .WithMany()
                .HasForeignKey(r => r.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasQueryFilter(r => !r.IsDeleted);
        });

        // ── AccountKeywordRule ──
        modelBuilder.Entity<AccountKeywordRule>(e =>
        {
            e.ToTable("account_keyword_rule");
            e.HasKey(r => r.Id);
            e.Property(r => r.Keyword).HasMaxLength(256).IsRequired();
            e.Property(r => r.NormalizedKeyword).HasMaxLength(256).IsRequired();
            e.Property(r => r.MatchType).HasConversion<string>().HasMaxLength(20).HasDefaultValue(KeywordMatchType.Contains);
            e.Property(r => r.TenantId).HasMaxLength(128);

            e.HasIndex(r => new { r.EntityId, r.NormalizedKeyword }).IsUnique();

            e.HasOne(r => r.Entity)
                .WithMany()
                .HasForeignKey(r => r.EntityId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(r => r.Account)
                .WithMany()
                .HasForeignKey(r => r.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasQueryFilter(r => !r.IsDeleted);
        });

        // ── LoanDetails ──
        modelBuilder.Entity<LoanDetails>(e =>
        {
            e.ToTable("loan_details");
            e.HasKey(l => l.Id);
            e.HasIndex(l => l.AccountId).IsUnique(); // one-to-one
            e.Property(l => l.LoanAmount).HasPrecision(18, 2);
            e.Property(l => l.InterestRate).HasPrecision(8, 6);
            e.Property(l => l.RepaymentAmount).HasPrecision(18, 2);
            e.Property(l => l.RedrawAvailable).HasPrecision(18, 2);
            e.Property(l => l.RateType).HasConversion<string>().HasMaxLength(20);
            e.Property(l => l.PaymentFrequency).HasConversion<string>().HasMaxLength(20);
            e.Property(l => l.TenantId).HasMaxLength(128);

            e.HasOne(l => l.PropertyAccount)
                .WithMany()
                .HasForeignKey(l => l.PropertyAccountId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(l => l.OffsetAccount)
                .WithMany()
                .HasForeignKey(l => l.OffsetAccountId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasMany(l => l.RateHistory)
                .WithOne(r => r.LoanDetails)
                .HasForeignKey(r => r.LoanDetailsId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasQueryFilter(l => !l.IsDeleted);
        });

        // ── LoanRateHistory ──
        modelBuilder.Entity<LoanRateHistory>(e =>
        {
            e.ToTable("loan_rate_history");
            e.HasKey(r => r.Id);
            e.Property(r => r.Rate).HasPrecision(8, 6);
            e.HasQueryFilter(r => !r.IsDeleted);
        });

        // ── Property ──
        modelBuilder.Entity<Property>(e =>
        {
            e.ToTable("property");
            e.HasKey(p => p.Id);
            e.HasIndex(p => p.AccountId).IsUnique(); // 1:1 with Account
            e.Property(p => p.Address).HasMaxLength(500).IsRequired();
            e.Property(p => p.Suburb).HasMaxLength(100);
            e.Property(p => p.State).HasMaxLength(100);
            e.Property(p => p.PostCode).HasMaxLength(20);
            e.Property(p => p.Country).HasMaxLength(3).IsRequired();
            e.Property(p => p.PropertyType).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(p => p.PurchasePrice).HasPrecision(18, 2);
            e.Property(p => p.LandSizeSqm).HasPrecision(10, 2);
            e.Property(p => p.FloorSizeSqm).HasPrecision(10, 2);
            e.Property(p => p.WeeklyRentTarget).HasPrecision(18, 2);
            e.Property(p => p.TenantId).HasMaxLength(128);

            e.HasOne(p => p.Account)
                .WithOne(a => a.Property)
                .HasForeignKey<Property>(p => p.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasQueryFilter(p => !p.IsDeleted);
        });

        // ── GlobalPriceCache ──
        modelBuilder.Entity<GlobalPriceCache>(e =>
        {
            e.ToTable("global_price_cache");
            e.HasKey(g => g.Id);
            e.HasIndex(g => new { g.Symbol, g.AsOfDate }).IsUnique();
            e.Property(g => g.Symbol).HasMaxLength(50).IsRequired();
            e.Property(g => g.InstrumentType).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(g => g.Price).HasPrecision(18, 8);
            e.Property(g => g.PriceUnit).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(g => g.Currency).HasMaxLength(3).IsRequired();
            e.Property(g => g.Source).HasConversion<string>().HasMaxLength(20).IsRequired();
            // No soft delete — this is infrastructure cache data, not user data
        });

        // ── UserDeletionRequest ──
        modelBuilder.Entity<UserDeletionRequest>(e =>
        {
            e.ToTable("user_deletion_requests");
            e.HasKey(r => r.Id);
            e.Property(r => r.Status).HasMaxLength(30).IsRequired();
            e.Property(r => r.RejectionReason).HasMaxLength(500);
            e.Property(r => r.TenantId).HasMaxLength(128);

            // FK to User — no cascade; we delete the user row manually in the service
            e.HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasQueryFilter(r => !r.IsDeleted);
        });

        // ── Tenant ──
        modelBuilder.Entity<Tenant>(e =>
        {
            e.ToTable("tenant");
            e.HasKey(t => t.Id);
            e.HasIndex(t => t.Name).IsUnique();
            e.Property(t => t.Name).HasMaxLength(256).IsRequired();
            e.Property(t => t.ContactEmail).HasMaxLength(256).IsRequired();
            e.Property(t => t.TenantId).HasMaxLength(128);
            e.HasQueryFilter(t => !t.IsDeleted);
        });

        // ── EntityIntegration ──
        modelBuilder.Entity<EntityIntegration>(e =>
        {
            e.ToTable("entity_integration");
            e.HasKey(ei => ei.Id);
            e.HasIndex(ei => new { ei.EntityId, ei.IntegrationType }).IsUnique();
            e.Property(ei => ei.IntegrationType).HasMaxLength(50).IsRequired();
            e.Property(ei => ei.ApiKey).HasMaxLength(1000); // encrypted, so longer
            e.Property(ei => ei.Settings).HasMaxLength(2000);
            e.Property(ei => ei.TenantId).HasMaxLength(128);

            e.HasOne(ei => ei.Entity)
                .WithMany(en => en.Integrations)
                .HasForeignKey(ei => ei.EntityId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasQueryFilter(ei => !ei.IsDeleted);
        });

        // ── PlatformIntegration ──
        modelBuilder.Entity<PlatformIntegration>(e =>
        {
            e.ToTable("platform_integration");
            e.HasKey(pi => pi.Id);
            e.HasIndex(pi => pi.IntegrationType).IsUnique();
            e.Property(pi => pi.IntegrationType).HasMaxLength(50).IsRequired();
            e.Property(pi => pi.ApiKey).HasMaxLength(1000); // encrypted
            e.Property(pi => pi.TenantId).HasMaxLength(128);
            e.HasQueryFilter(pi => !pi.IsDeleted);
        });

        // ── EntityDefaultTicker ──
        modelBuilder.Entity<EntityDefaultTicker>(e =>
        {
            e.ToTable("entity_default_ticker");
            e.HasKey(et => et.Id);
            e.HasIndex(et => new { et.EntityId, et.InstrumentId }).IsUnique();
            e.Property(et => et.TenantId).HasMaxLength(128);

            e.HasOne(et => et.Entity)
                .WithMany()
                .HasForeignKey(et => et.EntityId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(et => et.Instrument)
                .WithMany()
                .HasForeignKey(et => et.InstrumentId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasQueryFilter(et => !et.IsDeleted);
        });

        // ── FinancialConnection ──
        modelBuilder.Entity<FinancialConnection>(e =>
        {
            e.ToTable("financial_connection");
            e.HasKey(fc => fc.Id);
            e.HasIndex(fc => new { fc.EntityId, fc.ProviderConnectionId }).IsUnique();
            e.Property(fc => fc.Provider).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(fc => fc.ProviderConnectionId).HasMaxLength(256).IsRequired();
            e.Property(fc => fc.EncryptedCredential).HasMaxLength(2000).IsRequired(); // AES-256 encrypted
            e.Property(fc => fc.InstitutionId).HasMaxLength(100).IsRequired();
            e.Property(fc => fc.InstitutionName).HasMaxLength(256).IsRequired();
            e.Property(fc => fc.InstitutionLogoUrl).HasMaxLength(4000);
            e.Property(fc => fc.Status).HasConversion<string>().HasMaxLength(20).IsRequired().HasDefaultValue(ConnectionStatus.Active);
            e.Property(fc => fc.StatusMessage).HasMaxLength(500);
            e.Property(fc => fc.Country).HasMaxLength(2).IsRequired().HasDefaultValue("AU");
            e.Property(fc => fc.TenantId).HasMaxLength(128);

            e.HasOne(fc => fc.Entity)
                .WithMany(en => en.Connections)
                .HasForeignKey(fc => fc.EntityId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasQueryFilter(fc => !fc.IsDeleted);
        });

        // ── ConnectedAccount ──
        modelBuilder.Entity<ConnectedAccount>(e =>
        {
            e.ToTable("connected_account");
            e.HasKey(ca => ca.Id);
            e.HasIndex(ca => new { ca.ConnectionId, ca.ExternalAccountId }).IsUnique();
            e.Property(ca => ca.ExternalAccountId).HasMaxLength(256).IsRequired();
            e.Property(ca => ca.Name).HasMaxLength(256).IsRequired();
            e.Property(ca => ca.OfficialName).HasMaxLength(256);
            e.Property(ca => ca.Mask).HasMaxLength(20);
            e.Property(ca => ca.Type).HasMaxLength(50).IsRequired();
            e.Property(ca => ca.Subtype).HasMaxLength(50);
            e.Property(ca => ca.CurrentBalance).HasPrecision(18, 2);
            e.Property(ca => ca.AvailableBalance).HasPrecision(18, 2);
            e.Property(ca => ca.CurrencyCode).HasMaxLength(3).IsRequired();
            e.Property(ca => ca.TenantId).HasMaxLength(128);

            e.HasOne(ca => ca.Connection)
                .WithMany(fc => fc.ConnectedAccounts)
                .HasForeignKey(ca => ca.ConnectionId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(ca => ca.Entity)
                .WithMany()
                .HasForeignKey(ca => ca.EntityId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(ca => ca.LinkedAccount)
                .WithMany()
                .HasForeignKey(ca => ca.LinkedAccountId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasQueryFilter(ca => !ca.IsDeleted);
        });

        // ── ConnectionSyncLog ──
        modelBuilder.Entity<ConnectionSyncLog>(e =>
        {
            e.ToTable("connection_sync_log");
            e.HasKey(sl => sl.Id);
            e.HasIndex(sl => sl.ConnectionId);
            e.Property(sl => sl.Status).HasMaxLength(20).IsRequired();
            e.Property(sl => sl.ErrorMessage).HasMaxLength(1000);
            e.Property(sl => sl.TenantId).HasMaxLength(128);

            e.HasOne(sl => sl.Connection)
                .WithMany(fc => fc.SyncLogs)
                .HasForeignKey(sl => sl.ConnectionId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasQueryFilter(sl => !sl.IsDeleted);
        });

        // ── AuditLog ──
        // Does not extend BaseEntity — intentionally immutable, no soft delete.
        modelBuilder.Entity<AuditLog>(e =>
        {
            e.ToTable("audit_logs");
            e.HasKey(a => a.Id);
            e.Property(a => a.TenantId).HasMaxLength(128).IsRequired();
            e.Property(a => a.Action).HasMaxLength(100).IsRequired();
            e.Property(a => a.ObjectType).HasMaxLength(100);
            e.Property(a => a.ObjectId).HasMaxLength(256);
            e.Property(a => a.IpAddress).HasMaxLength(64);
            e.Property(a => a.UserAgent).HasMaxLength(512);
            e.HasIndex(a => new { a.TenantId, a.Timestamp });
            e.HasIndex(a => a.UserId);
        });

        // ── AgentFeatureFlag ──
        modelBuilder.Entity<AgentFeatureFlag>(e =>
        {
            e.ToTable("agent_feature_flags");
            e.HasKey(x => x.Id);
            e.Property(x => x.Key).HasMaxLength(100).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.SectionKey).HasConversion<string>().HasMaxLength(50);
            e.Property(x => x.TenantId).HasMaxLength(128);
            e.HasIndex(x => x.Key).IsUnique();
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        // ── AgentFeatureFlagAssignment ──
        modelBuilder.Entity<AgentFeatureFlagAssignment>(e =>
        {
            e.ToTable("agent_feature_flag_assignments");
            e.HasKey(x => x.Id);
            e.Property(x => x.TenantId).HasMaxLength(128);
            e.HasIndex(x => new { x.FeatureFlagId, x.UserId });

            e.HasOne(x => x.FeatureFlag)
                .WithMany(f => f.Assignments)
                .HasForeignKey(x => x.FeatureFlagId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired(false);

            e.HasOne(x => x.Entity)
                .WithMany()
                .HasForeignKey(x => x.EntityId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired(false);

            e.HasQueryFilter(x => !x.IsDeleted);
        });

        // ── AgentConversation ──
        modelBuilder.Entity<AgentConversation>(e =>
        {
            e.ToTable("agent_conversations");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(500).IsRequired();
            e.Property(x => x.ModelName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Provider).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.SectionType).HasConversion<string>().HasMaxLength(50);
            e.Property(x => x.TenantId).HasMaxLength(128);
            e.HasIndex(x => new { x.EntityId, x.UserId });

            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Entity)
                .WithMany()
                .HasForeignKey(x => x.EntityId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasQueryFilter(x => !x.IsDeleted);
        });

        // ── AgentMessage ──
        modelBuilder.Entity<AgentMessage>(e =>
        {
            e.ToTable("agent_messages");
            e.HasKey(x => x.Id);
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.SourceType).HasMaxLength(20);
            e.Property(x => x.TenantId).HasMaxLength(128);

            e.HasOne(x => x.Conversation)
                .WithMany(c => c.Messages)
                .HasForeignKey(x => x.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasQueryFilter(x => !x.IsDeleted);
        });

        // ── AgentContextSnapshot ──
        // No soft-delete filter — cache entries are replaced wholesale.
        modelBuilder.Entity<AgentContextSnapshot>(e =>
        {
            e.ToTable("agent_context_snapshots");
            e.HasKey(x => x.Id);
            e.Property(x => x.SnapshotType).HasMaxLength(50).IsRequired();
            e.Property(x => x.Hash).HasMaxLength(64);
            e.Property(x => x.TenantId).HasMaxLength(128);
            e.HasIndex(x => new { x.EntityId, x.SnapshotType });
        });

        // ── AgentInsight ──
        modelBuilder.Entity<AgentInsight>(e =>
        {
            e.ToTable("agent_insights");
            e.HasKey(x => x.Id);
            e.Property(x => x.InsightType).HasConversion<string>().HasMaxLength(50);
            e.Property(x => x.Severity).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.SourceType).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Title).HasMaxLength(300).IsRequired();
            e.Property(x => x.Summary).HasMaxLength(2000).IsRequired();
            e.Property(x => x.TenantId).HasMaxLength(128);
            e.HasIndex(x => new { x.EntityId, x.InsightType, x.IsDismissed });
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        // ── AgentAuditLog ──
        // Immutable, no soft-delete, does not extend BaseEntity.
        modelBuilder.Entity<AgentAuditLog>(e =>
        {
            e.ToTable("agent_audit_logs");
            e.HasKey(x => x.Id);
            e.Property(x => x.RequestType).HasMaxLength(30).IsRequired();
            e.Property(x => x.Provider).HasMaxLength(30).IsRequired();
            e.Property(x => x.Model).HasMaxLength(100).IsRequired();
            e.Property(x => x.PromptTemplateVersion).HasMaxLength(20).IsRequired();
            e.Property(x => x.SafetyDecision).HasMaxLength(20);
            e.HasIndex(x => new { x.UserId, x.CreatedAt });
        });

        // ── AgentWebResearchCache ──
        modelBuilder.Entity<AgentWebResearchCache>(e =>
        {
            e.ToTable("agent_web_research_cache");
            e.HasKey(x => x.Id);
            e.Property(x => x.TopicKey).HasMaxLength(100).IsRequired();
            e.Property(x => x.Query).HasMaxLength(500).IsRequired();
            e.Property(x => x.ProviderModel).HasMaxLength(100);
            e.Property(x => x.TenantId).HasMaxLength(128);
            e.HasIndex(x => x.TopicKey).IsUnique();
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        // ── AgentSavingsSnapshot ──
        modelBuilder.Entity<AgentSavingsSnapshot>(e =>
        {
            e.ToTable("agent_savings_snapshots");
            e.HasKey(x => x.Id);
            e.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            e.Property(x => x.TenantId).HasMaxLength(128);
            // One snapshot per entity per month — enforced in service via upsert
            e.HasIndex(x => new { x.EntityId, x.AsOfDate });
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        // ── AgentSetting ──
        modelBuilder.Entity<AgentSetting>(e =>
        {
            e.ToTable("agent_settings");
            e.HasKey(x => x.Id);
            e.Property(x => x.Key).HasMaxLength(100).IsRequired();
            e.Property(x => x.Value).HasMaxLength(500).IsRequired();
            e.HasIndex(x => x.Key).IsUnique();
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        // ── AgentScenario ──
        modelBuilder.Entity<AgentScenario>(e =>
        {
            e.ToTable("agent_scenarios");
            e.HasKey(x => x.Id);
            e.Property(x => x.ScenarioType).HasMaxLength(50).IsRequired();
            e.Property(x => x.Title).HasMaxLength(300).IsRequired();
            e.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            e.HasIndex(x => new { x.EntityId, x.UserId });
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        // ── AgentDigestEmail ──
        modelBuilder.Entity<AgentDigestEmail>(e =>
        {
            e.ToTable("agent_digest_emails");
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.WeekKey).HasMaxLength(20).IsRequired();
            e.HasIndex(x => new { x.EntityId, x.WeekKey });
            e.HasQueryFilter(x => !x.IsDeleted);
        });
    }

    public override int SaveChanges()
    {
        HandleSoftDeletes();
        SetTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        HandleSoftDeletes();
        SetTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void HandleSoftDeletes()
    {
        var deletedEntries = ChangeTracker.Entries<BaseEntity>()
            .Where(e => e.State == EntityState.Deleted);

        foreach (var entry in deletedEntries)
        {
            entry.State = EntityState.Modified;
            entry.Entity.IsDeleted = true;
            entry.Entity.UpdatedAt = DateTime.UtcNow;
        }
    }

    private void SetTimestamps()
    {
        var entries = ChangeTracker.Entries<BaseEntity>();
        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = DateTime.UtcNow;
        }
    }
}
