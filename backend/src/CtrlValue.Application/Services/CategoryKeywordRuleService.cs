using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain.Entities;
using CtrlValue.Domain.Enums;
using CtrlValue.Infrastructure.Data;

namespace CtrlValue.Application.Services;

public class CategoryKeywordRuleService : ICategoryKeywordRuleService
{
    private readonly AppDbContext _context;

    public CategoryKeywordRuleService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<CategoryKeywordRuleDto>> GetByCategoryAsync(Guid entityId, Guid categoryId)
    {
        return await _context.CategoryKeywordRules
            .Include(r => r.Category)
            .Where(r => r.EntityId == entityId && r.CategoryId == categoryId)
            .Select(r => MapToDto(r))
            .ToListAsync();
    }

    public async Task<List<CategoryKeywordRuleDto>> GetAllAsync(Guid entityId)
    {
        return await _context.CategoryKeywordRules
            .Include(r => r.Category)
            .Where(r => r.EntityId == entityId)
            .OrderBy(r => r.Category.Name)
            .ThenBy(r => r.Keyword)
            .Select(r => MapToDto(r))
            .ToListAsync();
    }

    public async Task<CategoryKeywordRuleDto?> GetByIdAsync(Guid entityId, Guid ruleId)
    {
        var rule = await _context.CategoryKeywordRules
            .Include(r => r.Category)
            .FirstOrDefaultAsync(r => r.Id == ruleId && r.EntityId == entityId);

        return rule != null ? MapToDto(rule) : null;
    }

    public async Task<CategoryKeywordRuleDto> CreateAsync(Guid entityId, CreateCategoryKeywordRuleRequest request, Guid? userId = null)
    {
        if (string.IsNullOrWhiteSpace(request.Keyword))
            throw new ArgumentException("Keyword is required.");

        var normalized = request.Keyword.Trim().ToUpperInvariant();

        // Check if keyword already exists for this entity
        var exists = await _context.CategoryKeywordRules
            .AnyAsync(r => r.EntityId == entityId && r.NormalizedKeyword == normalized);

        if (exists)
        {
            throw new InvalidOperationException($"Keyword '{normalized}' already exists under another category.");
        }

        var rule = new CategoryKeywordRule
        {
            EntityId = entityId,
            CategoryId = request.CategoryId,
            Keyword = request.Keyword.Trim(),
            NormalizedKeyword = normalized,
            MatchType = request.MatchType,
            IsCaseSensitive = request.IsCaseSensitive,
            CreatedByUserId = userId
        };

        _context.CategoryKeywordRules.Add(rule);
        await _context.SaveChangesAsync();

        // Auto-apply: back-fill any uncategorized transactions that match the new rule
        await ApplyRulesToWorkspaceAsync(entityId);

        // Reload to get category name
        var savedRule = await _context.CategoryKeywordRules
            .Include(r => r.Category)
            .FirstAsync(r => r.Id == rule.Id);

        return MapToDto(savedRule);
    }

    public async Task<CategoryKeywordRuleDto> UpdateAsync(Guid entityId, Guid ruleId, UpdateCategoryKeywordRuleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Keyword))
            throw new ArgumentException("Keyword is required.");

        var rule = await _context.CategoryKeywordRules
            .Include(r => r.Category)
            .FirstOrDefaultAsync(r => r.Id == ruleId && r.EntityId == entityId);

        if (rule == null)
            throw new KeyNotFoundException("Keyword rule not found.");

        var normalized = request.Keyword.Trim().ToUpperInvariant();

        // Check if keyword already exists for this entity (excluding current rule)
        var exists = await _context.CategoryKeywordRules
            .AnyAsync(r => r.EntityId == entityId && r.NormalizedKeyword == normalized && r.Id != ruleId);

        if (exists)
        {
            throw new InvalidOperationException($"Keyword '{normalized}' already exists under another category.");
        }

        rule.CategoryId = request.CategoryId;
        rule.Keyword = request.Keyword.Trim();
        rule.NormalizedKeyword = normalized;
        rule.MatchType = request.MatchType;
        rule.IsCaseSensitive = request.IsCaseSensitive;
        rule.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Auto-apply: back-fill any uncategorized transactions that match the updated rule
        await ApplyRulesToWorkspaceAsync(entityId);

        // Reload to get category name if it changed
        var updatedRule = await _context.CategoryKeywordRules
            .Include(r => r.Category)
            .FirstAsync(r => r.Id == rule.Id);

        return MapToDto(updatedRule);
    }

    public async Task DeleteAsync(Guid entityId, Guid ruleId)
    {
        var rule = await _context.CategoryKeywordRules
            .FirstOrDefaultAsync(r => r.Id == ruleId && r.EntityId == entityId);

        if (rule != null)
        {
            _context.CategoryKeywordRules.Remove(rule);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<Guid?> MatchCategoryAsync(Guid entityId, string description)
    {
        if (string.IsNullOrWhiteSpace(description)) return null;

        var rules = await _context.CategoryKeywordRules
            .Where(r => r.EntityId == entityId)
            .OrderByDescending(r => r.Keyword.Length)
            .ToListAsync();

        return MatchFromRules(rules, description);
    }

    public async Task<int> ApplyRulesToWorkspaceAsync(Guid entityId)
    {
        // Early-exit if nothing to do
        var hasUncategorized = await _context.Transactions
            .AnyAsync(t => t.EntityId == entityId && t.CategoryId == null && !t.IsDeleted);

        if (!hasUncategorized) return 0;

        // Load all rules once (longest-wins order)
        var rules = await _context.CategoryKeywordRules
            .Where(r => r.EntityId == entityId)
            .OrderByDescending(r => r.Keyword.Length)
            .ToListAsync();

        if (rules.Count == 0) return 0;

        // Load all uncategorized transactions (minimal projection)
        var uncategorized = await _context.Transactions
            .Where(t => t.EntityId == entityId && t.CategoryId == null && !t.IsDeleted)
            .Select(t => new { t.Id, t.Description, t.Merchant })
            .ToListAsync();

        // Match each transaction against rules
        var matchesByCategory = new Dictionary<Guid, List<Guid>>();
        foreach (var txn in uncategorized)
        {
            var categoryId = MatchFromRules(rules, txn.Description);
            if (categoryId == null && !string.IsNullOrWhiteSpace(txn.Merchant))
                categoryId = MatchFromRules(rules, txn.Merchant);

            if (categoryId == null) continue;

            if (!matchesByCategory.TryGetValue(categoryId.Value, out var ids))
            {
                ids = new List<Guid>();
                matchesByCategory[categoryId.Value] = ids;
            }
            ids.Add(txn.Id);
        }

        if (matchesByCategory.Count == 0) return 0;

        // Bulk update grouped by category to minimise round-trips
        var totalUpdated = 0;
        foreach (var (categoryId, ids) in matchesByCategory)
        {
            var updated = await _context.Transactions
                .Where(t => ids.Contains(t.Id))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.CategoryId, categoryId)
                    .SetProperty(t => t.UpdatedAt, DateTime.UtcNow));
            totalUpdated += updated;
        }

        return totalUpdated;
    }

    public async Task<CategorySuggestionDto?> SuggestCategoryAsync(Guid entityId, string description)
    {
        if (string.IsNullOrWhiteSpace(description)) return null;

        var rules = await _context.CategoryKeywordRules
            .Include(r => r.Category)
            .Where(r => r.EntityId == entityId && r.Category.IsActive)
            .OrderByDescending(r => r.Keyword.Length)
            .ToListAsync();

        var normalizedDesc = description.ToUpperInvariant();

        foreach (var rule in rules)
        {
            if (RuleMatches(rule, description, normalizedDesc))
            {
                return new CategorySuggestionDto
                {
                    CategoryId = rule.CategoryId,
                    CategoryName = rule.Category?.Name ?? string.Empty,
                    MatchedKeyword = rule.Keyword
                };
            }
        }

        return null;
    }

    // Matches a single text value against a pre-loaded, pre-sorted rule list.
    // Caller is responsible for sorting rules by keyword length DESC before passing in.
    private static Guid? MatchFromRules(List<CategoryKeywordRule> rules, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var normalizedText = text.ToUpperInvariant();

        foreach (var rule in rules)
        {
            if (RuleMatches(rule, text, normalizedText))
                return rule.CategoryId;
        }

        return null;
    }

    private static bool RuleMatches(CategoryKeywordRule rule, string original, string normalized)
    {
        var keywordToCheck = rule.IsCaseSensitive ? rule.Keyword : rule.NormalizedKeyword;
        var textToCheck = rule.IsCaseSensitive ? original : normalized;

        return rule.MatchType switch
        {
            KeywordMatchType.Exact => textToCheck.Equals(keywordToCheck, StringComparison.Ordinal),
            KeywordMatchType.StartsWith => textToCheck.StartsWith(keywordToCheck, StringComparison.Ordinal),
            KeywordMatchType.Regex => TryRegexMatch(rule.Keyword, original, rule.IsCaseSensitive),
            _ => textToCheck.Contains(keywordToCheck) // Contains (default)
        };
    }

    private static bool TryRegexMatch(string pattern, string input, bool caseSensitive)
    {
        try
        {
            var options = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
            return Regex.IsMatch(input, pattern, options, TimeSpan.FromMilliseconds(100));
        }
        catch
        {
            return false;
        }
    }

    private static CategoryKeywordRuleDto MapToDto(CategoryKeywordRule rule)
    {
        return new CategoryKeywordRuleDto
        {
            Id = rule.Id,
            EntityId = rule.EntityId,
            CategoryId = rule.CategoryId,
            CategoryName = rule.Category?.Name ?? string.Empty,
            Keyword = rule.Keyword,
            NormalizedKeyword = rule.NormalizedKeyword,
            MatchType = rule.MatchType,
            IsCaseSensitive = rule.IsCaseSensitive,
            CreatedAt = rule.CreatedAt,
            UpdatedAt = rule.UpdatedAt
        };
    }
}
