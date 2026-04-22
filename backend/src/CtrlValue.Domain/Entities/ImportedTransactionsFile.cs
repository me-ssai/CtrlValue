using CtrlValue.Domain.Enums;

namespace CtrlValue.Domain.Entities;

public class ImportedTransactionsFile : BaseEntity
{
    public Guid EntityId { get; set; }
    
    /// <summary>The primary account the QIF file is imported into.</summary>
    public Guid AccountId { get; set; }
    
    public string OriginalFilename { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    
    public ImportStatus Status { get; set; } = ImportStatus.Staged;
    
    public bool AllowDuplicates { get; set; } = false;
    
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int DuplicateRows { get; set; }
    public int AlreadyImportedRows { get; set; }
    public int ErrorRows { get; set; }
    
    // Navigation properties
    public Entity Entity { get; set; } = null!;
    public Account Account { get; set; } = null!;
    public ICollection<ImportedTransactionsFileStaging> StagingRows { get; set; } = new List<ImportedTransactionsFileStaging>();
}
