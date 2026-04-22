using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain.Entities;
using CtrlValue.Domain.Enums;
using CtrlValue.Infrastructure.Data;

namespace CtrlValue.Application.Services;

public class AccountKeywordRuleService : IAccountKeywordRuleService
{
    private readonly AppDbContext _context;

    public AccountKeywordRuleService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<AccountKeywordRuleDto>> GetAllAsync(Guid entityId)
    {
        return await _context.AccountKeywordRules
            .Include(r => r.Account)
            .Where(r => r.EntityId == entityId)
            .OrderBy(r => r.Account.Name)
            .ThenBy(r => r.Keyword)
            .Select(r => MapToDto(r))
            .ToListAsync();
    }

    public async Task<AccountKeywordRuleDto?> GetByIdAsync(Guid entityId, Guid ruleId)
    {
        var rule = await _context.AccountKeywordRules
            .Include(r => r.Account)
            .FirstOrDefaultAsync(r => r.Id == ruleId && r.EntityId == entityId);

        return rule != null ? MapToDto(rule) : null;
    }

    public async Task<List<AccountKeywordRuleDto>> GetByAccountAsync(Guid entityId, Guid accountId)
    {
        return await _context.AccountKeywordRules
            .Include(r => r.Account)
            .Where(r => r.EntityId == entityId && r.AccountId == accountId)
            .Select(r => MapToDto(r))
            .ToListAsync();
    }

    public async Task<AccountKeywordRuleDto> CreateAsync(Guid entityId, CreateAccountKeywordRuleRequest request, Guid? userId = null)
    {
        if (string.IsNullOrWhiteSpace(request.Keyword))
            throw new ArgumentException("Keyword is required.");

        var normalized = request.Keyword.Trim().ToUpperInvariant();

        var exists = await _context.AccountKeywordRules
            .AnyAsync(r => r.EntityId == entityId && r.NormalizedKeyword == normalized);

        if (exists)
            throw new InvalidOperationException($"Keyword '{normalized}' already exists under another account.");

        var rule = new AccountKeywordRule
        {
            EntityId          = entityId,
            AccountId         = request.AccountId,
            Keyword           = request.Keyword.Trim(),
            NormalizedKeyword = normalized,
            MatchType         = request.MatchType,
            IsCaseSensitive   = request.IsCaseSensitive,
            CreatedByUserId   = userId
        };

        _context.AccountKeywordRules.Add(rule);
        await _context.SaveChangesAsync();

        var savedRule = await _context.AccountKeywordRules
            .Include(r => r.Account)
            .FirstAsync(r => r.Id == rule.Id);

        return MapToDto(savedRule);
    }

    public async Task<AccountKeywordRuleDto> UpdateAsync(Guid entityId, Guid ruleId, UpdateAccountKeywordRuleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Keyword))
            throw new ArgumentException("Keyword is required.");

        var rule = await _context.AccountKeywordRules
            .Include(r => r.Account)
            .FirstOrDefaultAsync(r => r.Id == ruleId && r.EntityId == entityId);

        if (rule == null)
            throw new KeyNotFoundException("Account keyword rule not found.");

        var normalized = request.Keyword.Trim().ToUpperInvariant();

        var exists = await _context.AccountKeywordRules
            .AnyAsync(r => r.EntityId == entityId && r.NormalizedKeyword == normalized && r.Id != ruleId);

        if (exists)
            throw new InvalidOperationException($"Keyword '{normalized}' already exists under another account.");

        rule.AccountId         = request.AccountId;
        rule.Keyword           = request.Keyword.Trim();
        rule.NormalizedKeyword = normalized;
        rule.MatchType         = request.MatchType;
        rule.IsCaseSensitive   = request.IsCaseSensitive;
        rule.UpdatedAt         = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var updatedRule = await _context.AccountKeywordRules
            .Include(r => r.Account)
            .FirstAsync(r => r.Id == rule.Id);

        return MapToDto(updatedRule);
    }

    public async Task DeleteAsync(Guid entityId, Guid ruleId)
    {
        var rule = await _context.AccountKeywordRules
            .FirstOrDefaultAsync(r => r.Id == ruleId && r.EntityId == entityId);

        if (rule != null)
        {
            _context.AccountKeywordRules.Remove(rule);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<Guid?> MatchAccountAsync(Guid entityId, string description)
    {
        if (string.IsNullOrWhiteSpace(description)) return null;

        var rules = await _context.AccountKeywordRules
            .Where(r => r.EntityId == entityId)
            .ToListAsync();

        return MatchFromRules(rules, description);
    }

    private static Guid? MatchFromRules(List<AccountKeywordRule> rules, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var normalizedText = text.ToUpperInvariant();

        var bestAccountId = (Guid?)null;
        var bestPosition  = int.MaxValue;

        foreach (var rule in rules)
        {
            var position = GetMatchPosition(rule, text, normalizedText);
            if (position >= 0 && position < bestPosition)
            {
                bestPosition  = position;
                bestAccountId = rule.AccountId;
            }
        }

        return bestAccountId;
    }

    private static int GetMatchPosition(AccountKeywordRule rule, string original, string normalized)
    {
        var keywordToCheck = rule.IsCaseSensitive ? rule.Keyword : rule.NormalizedKeyword;
        var textToCheck    = rule.IsCaseSensitive ? original : normalized;

        return rule.MatchType switch
        {
            KeywordMatchType.Exact      => textToCheck.Equals(keywordToCheck, StringComparison.Ordinal) ? 0 : -1,
            KeywordMatchType.StartsWith => textToCheck.StartsWith(keywordToCheck, StringComparison.Ordinal) ? 0 : -1,
            KeywordMatchType.Regex      => GetRegexMatchPosition(rule.Keyword, original, rule.IsCaseSensitive),
            _                           => textToCheck.IndexOf(keywordToCheck, StringComparison.Ordinal)
        };
    }

    private static int GetRegexMatchPosition(string pattern, string input, bool caseSensitive)
    {
        try
        {
            var options = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
            var match   = Regex.Match(input, pattern, options, TimeSpan.FromMilliseconds(100));
            return match.Success ? match.Index : -1;
        }
        catch
        {
            return -1;
        }
    }

    private static AccountKeywordRuleDto MapToDto(AccountKeywordRule rule) => new()
    {
        Id                = rule.Id,
        EntityId          = rule.EntityId,
        AccountId         = rule.AccountId,
        AccountName       = rule.Account?.Name ?? string.Empty,
        Keyword           = rule.Keyword,
        NormalizedKeyword = rule.NormalizedKeyword,
        MatchType         = rule.MatchType,
        IsCaseSensitive   = rule.IsCaseSensitive,
        CreatedAt         = rule.CreatedAt,
        UpdatedAt         = rule.UpdatedAt
    };
}
