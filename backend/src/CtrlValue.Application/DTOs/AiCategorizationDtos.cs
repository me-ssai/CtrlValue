namespace CtrlValue.Application.DTOs;

// ═══════════════════════════════════════════════════════════════════════════
// AI Categorisation DTOs
// Internal use only — not exposed via API controllers.
// ═══════════════════════════════════════════════════════════════════════════

// ── Outbound: sent to ProjectZAI ────────────────────────────────────────────

/// <summary>
/// Represents a single transaction as sent to the AI for categorisation.
/// </summary>
public class AiTransactionPayload
{
    /// <summary>Staging row Id as a string (used to correlate the AI response).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Transaction date in yyyy-MM-dd format.</summary>
    public string Date { get; set; } = string.Empty;

    /// <summary>Absolute (unsigned) amount.</summary>
    public decimal Amount { get; set; }

    /// <summary>"Inflow" or "Outflow".</summary>
    public string Direction { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

/// <summary>
/// Represents a single category option sent to the AI.
/// </summary>
public class AiCategoryOption
{
    /// <summary>Category Id as a string.</summary>
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>e.g. "Expense", "Income".</summary>
    public string Type { get; set; } = string.Empty;

    public string? ParentName { get; set; }
}

// ── Inbound: received from ProjectZAI ───────────────────────────────────────

/// <summary>
/// Represents a single suggested categorisation returned by the AI.
/// </summary>
public class AiCategorizationResult
{
    /// <summary>Matches <see cref="AiTransactionPayload.Id"/> of the corresponding transaction.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Category Id string (Guid), or null if the AI could not determine a category.</summary>
    public string? CategoryId { get; set; }

    /// <summary>Human-readable category name — used for logging/debug only.</summary>
    public string? CategoryName { get; set; }

    /// <summary>Optional confidence hint: "high", "medium", or "low".</summary>
    public string? Confidence { get; set; }
}
