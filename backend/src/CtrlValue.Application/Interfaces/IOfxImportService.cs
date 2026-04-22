using CtrlValue.Application.DTOs;

namespace CtrlValue.Application.Interfaces;

public interface IOfxImportService
{
    /// <summary>Parse the OFX stream, run duplicate detection, and bulk-insert staging rows.</summary>
    Task<OfxImportedTransactionsFileDto> UploadAndStageAsync(
        Guid entityId,
        Guid accountId,
        bool allowDuplicates,
        Stream fileStream,
        string filename);

    /// <summary>Return staged rows grouped by status with summary counts.</summary>
    Task<OfxStagedImportReviewDto> GetStagedImportAsync(Guid fileId, Guid entityId);

    /// <summary>Update a single staging row's ToAccountId, FromAccountId, and/or CategoryId.</summary>
    Task<OfxStagingRowDto> UpdateStagingRowAsync(
        Guid fileId,
        Guid rowId,
        UpdateStagingRowRequest request,
        Guid entityId);

    /// <summary>Atomically insert valid staging rows into the transactions table and mark the file as confirmed.</summary>
    Task<OfxImportedTransactionsFileDto> CommitImportAsync(Guid fileId, Guid entityId);

    /// <summary>List all import files for an entity.</summary>
    Task<List<OfxImportedTransactionsFileDto>> GetImportFilesAsync(Guid entityId);

    /// <summary>Soft-delete an import file and its staging rows.</summary>
    Task DeleteImportFileAsync(Guid fileId, Guid entityId);
}
