using Microsoft.EntityFrameworkCore;
using CtrlValue.Domain.Entities;
using CtrlValue.Domain.Enums;
using CtrlValue.Infrastructure.Data;

namespace CtrlValue.Infrastructure.Data.Seeders;

/// <summary>
/// Seeds the standard 14 categories and their keyword rules for a newly created entity.
/// Idempotent — safe to call more than once; existing categories are not duplicated.
/// </summary>
public class DefaultCategorySeeder : IDefaultCategorySeeder
{
    private readonly AppDbContext _db;

    public DefaultCategorySeeder(AppDbContext db)
    {
        _db = db;
    }

    // ── Seeding data ──────────────────────────────────────────────────────────

    private record CategorySeed(string Name, CategoryType Type, string[] Keywords);

    private static readonly CategorySeed[] _categories =
    [
        new("Salary",        CategoryType.INCOME,   ["SALARY","PAYROLL","WAGES","PAYRUN","PAYMENT FROM EMPLOYER","EMPLOYER PAY","CREDIT PAYROLL"]),
        new("Dividends",     CategoryType.INCOME,   ["DIVIDEND","DIV","DIV PAYMENT","DISTRIBUTION","ETF DISTRIBUTION","SHARE DIVIDEND","DRP CASH"]),
        new("Interest",      CategoryType.INCOME,   ["INTEREST","INTEREST PAID","INTEREST RECEIVED","BONUS INTEREST","SAVINGS INTEREST","CREDIT INTEREST"]),
        new("Deposit",       CategoryType.INCOME,   ["CASH DEPOSIT","ATM DEPOSIT","BRANCH DEPOSIT","DEPOSIT","CASH DEP","CDM DEPOSIT"]),
        new("Transfer",      CategoryType.TRANSFER, ["TRANSFER","INTERNAL TRANSFER","ACCOUNT TRANSFER","BANK TRANSFER","OSKO","PAYID","NPP","FAST PAYMENT","ONLINE TRANSFER","XFER"]),
        new("Rent",          CategoryType.EXPENSE,  ["RENT","RENT PAYMENT","REAL ESTATE","PROPERTY MANAGEMENT","PROPERTY MGMT","RENTAL","LEASE PAYMENT","RAY WHITE","BARRY PLANT","LJ HOOKER"]),
        new("Groceries",     CategoryType.EXPENSE,  ["WOOLWORTHS","COLES","ALDI","IGA","COSTCO","HARRIS FARM","FOODWORKS","SPUDSHED","FRUIT WORLD","SUPERMARKET","GROCERY"]),
        new("Utilities",     CategoryType.EXPENSE,  ["AGL","ORIGIN","ENERGYAUSTRALIA","RED ENERGY","POWERSHOP","SIMPLY ENERGY","TELSTRA","OPTUS","VODAFONE","DODO","AUSSIE BROADBAND","NBN","WATER","GAS","ELECTRICITY","UTILITY","UTILITIES"]),
        new("Transport",     CategoryType.EXPENSE,  ["UBER","UBER TRIP","DIDI","OLA","13CABS","CABCHARGE","SHELL","BP","AMPOL","CALTEX","7-ELEVEN","MYKI","OPAL","PTV","TRANSURBAN","CITYLINK","EASTLINK","TOLL","PETROL","FUEL","SERVO"]),
        new("Dining",        CategoryType.EXPENSE,  ["MCDONALDS","KFC","HUNGRY JACKS","SUBWAY","DOMINOS","PIZZA HUT","GRILLD","GUZMAN","GUZMAN Y GOMEZ","CAFE","RESTAURANT","DINING","UBER EATS","MENULOG","DOORDASH"]),
        new("Subscriptions", CategoryType.EXPENSE,  ["NETFLIX","SPOTIFY","DISNEY","DISNEY PLUS","APPLE.COM/BILL","APPLE BILL","GOOGLE","GOOGLE PLAY","MICROSOFT","ADOBE","CANVA","YOUTUBE PREMIUM","AMAZON PRIME","ICLOUD","DROPBOX","CHATGPT","OPENAI"]),
        new("Insurance",     CategoryType.EXPENSE,  ["INSURANCE","AAMI","GIO","NRMA","ALLIANZ","BUPA","MEDIBANK","NIB","YOUI","CGU","APIA","HCF"]),
        new("Shopping",      CategoryType.EXPENSE,  ["AMAZON","EBAY","KMART","TARGET","BIG W","MYER","DAVID JONES","JB HI-FI","JBHIFI","HARVEY NORMAN","IKEA","OFFICEWORKS","ETSY","THE ICONIC","SHOPPING","RETAIL"]),
        new("Other Expense", CategoryType.EXPENSE,  ["BANK FEE","ACCOUNT FEE","MONTHLY FEE","SERVICE FEE","ATM FEE","OVERDRAFT FEE","LATE FEE","FOREIGN TRANSACTION FEE","CHARGE","BANK CHARGE","WITHDRAWAL","ATM WITHDRAWAL"]),
    ];

    public async Task SeedAsync(Guid entityId, string tenantId, CancellationToken ct = default)
    {
        // Load existing category names for this entity to guard against duplicates
        var existingNames = new HashSet<string>(await _db.Categories
            .Where(c => c.EntityId == entityId && !c.IsDeleted)
            .Select(c => c.Name)
            .ToListAsync(ct));

        var now = DateTime.UtcNow;

        foreach (var seed in _categories)
        {
            if (existingNames.Contains(seed.Name))
                continue;

            var category = new Category
            {
                EntityId     = entityId,
                TenantId     = tenantId,
                Name         = seed.Name,
                CategoryType = seed.Type,
                IsActive     = true,
                CreatedAt    = now,
            };
            _db.Categories.Add(category);

            foreach (var keyword in seed.Keywords)
            {
                _db.CategoryKeywordRules.Add(new CategoryKeywordRule
                {
                    EntityId          = entityId,
                    TenantId          = tenantId,
                    CategoryId        = category.Id,
                    Keyword           = keyword,
                    NormalizedKeyword = keyword.ToUpperInvariant(),
                    MatchType         = KeywordMatchType.Contains,
                    IsCaseSensitive   = false,
                    CreatedAt         = now,
                });
            }
        }

        await _db.SaveChangesAsync(ct);
    }
}
