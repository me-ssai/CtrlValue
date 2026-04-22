namespace CtrlValue.Application.DTOs;

// ═══════════════════════════════════════════════════════════════════════════
// OFX Import DTOs
// All OFX types extend their QIF counterparts so that shared orchestration
// (review screen, commit, etc.) continues to work against the same base types.
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Staging row DTO returned by the OFX import pipeline.
/// Extends <see cref="StagingRowDto"/> with OFX-specific fields.
/// </summary>
public class OfxStagingRowDto : StagingRowDto
{
    /// <summary>Raw FITID from the OFX file. Null if the file did not include one.</summary>
    public string? ExternalId { get; set; }

    /// <summary>Currency code from OFX CURDEF (e.g. AUD, USD).</summary>
    public string? Currency { get; set; }

    /// <summary>OFX TRNTYPE (DEBIT, CREDIT, etc.). Stored for diagnostics; not used for direction logic.</summary>
    public string? OfxTrnType { get; set; }
}

/// <summary>
/// File-level DTO returned by the OFX import pipeline.
/// Extends <see cref="ImportedTransactionsFileDto"/> with an optional non-fatal warning.
/// </summary>
public class OfxImportedTransactionsFileDto : ImportedTransactionsFileDto
{
    /// <summary>
    /// Non-fatal warning produced during parsing (e.g. "Multiple statement blocks found —
    /// only the first was imported"). Null when nothing noteworthy occurred.
    /// </summary>
    public string? ImportWarning { get; set; }
}

/// <summary>
/// Review wrapper returned by GET api/ofx-import/{fileId}/staged.
/// Mirrors <see cref="StagedImportReviewDto"/> but uses OFX-typed rows and file.
/// </summary>
public class OfxStagedImportReviewDto
{
    public OfxImportedTransactionsFileDto File { get; set; } = null!;
    public List<OfxStagingRowDto> ValidRows { get; set; } = new();
    public List<OfxStagingRowDto> DuplicateRows { get; set; } = new();
    public List<OfxStagingRowDto> AlreadyImportedRows { get; set; } = new();
    public List<OfxStagingRowDto> ErrorRows { get; set; } = new();
}
