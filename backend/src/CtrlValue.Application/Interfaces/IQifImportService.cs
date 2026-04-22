using CtrlValue.Application.DTOs;

namespace CtrlValue.Application.Interfaces;

public interface IQifImportService
{
    /// <summary>Parse the QIF stream, compute hashes, run duplicate detection, bulk-insert staging rows.</summary>
    Task<ImportedTransactionsFileDto> UploadAndStageAsync(
        Guid entityId,
        Guid accountId,
        bool allowDuplicates,
        string? dateFormat,
        Stream fileStream,
        string filename);

    /// <summary>Return staged rows grouped by status with summary counts.</summary>
    Task<StagedImportReviewDto> GetStagedImportAsync(Guid fileId, Guid entityId);

    /// <summary>Update a single staging row's ToAccountId, FromAccountId, and/or CategoryId.</summary>
    Task<StagingRowDto> UpdateStagingRowAsync(
        Guid fileId,
        Guid rowId,
        UpdateStagingRowRequest request,
        Guid entityId);

    /// <summary>Atomically insert valid staging rows into the transactions table and mark the file as confirmed.</summary>
    Task<ImportedTransactionsFileDto> CommitImportAsync(Guid fileId, Guid entityId);

    /// <summary>List all import files for an entity.</summary>
    Task<List<ImportedTransactionsFileDto>> GetImportFilesAsync(Guid entityId);

    /// <summary>Soft-delete an import file and its staging rows.</summary>
    Task DeleteImportFileAsync(Guid fileId, Guid entityId);
}
