using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain;

namespace CtrlValue.Api.Controllers;

/// <summary>Request model for QIF file upload.</summary>
public class QifUploadRequest
{
    /// <summary>The .qif file to import.</summary>
    public IFormFile File { get; set; } = null!;
    /// <summary>ID of the account to import transactions into.</summary>
    public Guid AccountId { get; set; }
    /// <summary>If true, transactions already in the system will still be imported.</summary>
    public bool AllowDuplicates { get; set; } = false;
    /// <summary>Optional user-selected date format (e.g. DD/MM/YYYY).</summary>
    public string? DateFormat { get; set; }
}

[ApiController]
[Route("api/qif-import")]
[Authorize]
public class QifImportController : EntityContextController
{
    private readonly IQifImportService _qifImportService;

    public QifImportController(IQifImportService qifImportService, IEntityService entityService, IPermissionService permissions)
        : base(entityService, permissions)
    {
        _qifImportService = qifImportService;
    }

    /// <summary>Upload a .qif file, parse it, and stage all transactions for review.</summary>
    [HttpPost("upload")]
    [RequestSizeLimit(10_485_760)] // 10 MB
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ImportedTransactionsFileDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ImportedTransactionsFileDto>> Upload([FromForm] QifUploadRequest request)
    {
        if (request.File == null || request.File.Length == 0)
            return BadRequest(new { error = "No file provided." });

        var extension = Path.GetExtension(request.File.FileName).ToLowerInvariant();
        if (extension != ".qif")
            return BadRequest(new { error = "Only .qif files are supported." });

        var entityId = await ResolveEntityIdAsync();
        await RequirePermissionAsync(entityId, Permissions.Transactions.Write);

        await using var stream = request.File.OpenReadStream();
        var result = await _qifImportService.UploadAndStageAsync(
            entityId,
            request.AccountId,
            request.AllowDuplicates,
            request.DateFormat,
            stream,
            request.File.FileName);

        return CreatedAtAction(nameof(GetStagedImport), new { fileId = result.Id }, result);
    }

    /// <summary>List all import files for the current entity.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<ImportedTransactionsFileDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ImportedTransactionsFileDto>>> GetImportFiles()
    {
        var entityId = await ResolveEntityIdAsync();
        var files = await _qifImportService.GetImportFilesAsync(entityId);
        return Ok(files);
    }

    /// <summary>Get the staged import review data (rows grouped by status).</summary>
    [HttpGet("{fileId:guid}/staged")]
    [ProducesResponseType(typeof(StagedImportReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StagedImportReviewDto>> GetStagedImport(Guid fileId)
    {
        var entityId = await ResolveEntityIdAsync();
        var review = await _qifImportService.GetStagedImportAsync(fileId, entityId);
        return Ok(review);
    }

    /// <summary>Update a single staging row's account and/or category selection.</summary>
    [HttpPut("{fileId:guid}/staging/{rowId:guid}")]
    [ProducesResponseType(typeof(StagingRowDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StagingRowDto>> UpdateStagingRow(
        Guid fileId,
        Guid rowId,
        [FromBody] UpdateStagingRowRequest request)
    {
        var entityId = await ResolveEntityIdAsync();
        await RequirePermissionAsync(entityId, Permissions.Transactions.Write);
        var row = await _qifImportService.UpdateStagingRowAsync(fileId, rowId, request, entityId);
        return Ok(row);
    }

    /// <summary>Commit all valid staging rows into the transactions table.</summary>
    [HttpPost("{fileId:guid}/commit")]
    [ProducesResponseType(typeof(ImportedTransactionsFileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ImportedTransactionsFileDto>> CommitImport(Guid fileId)
    {
        var entityId = await ResolveEntityIdAsync();
        await RequirePermissionAsync(entityId, Permissions.Transactions.Write);
        var result = await _qifImportService.CommitImportAsync(fileId, entityId);
        return Ok(result);
    }

    /// <summary>Soft-delete an import file and all its staging rows.</summary>
    [HttpDelete("{fileId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteImportFile(Guid fileId)
    {
        var entityId = await ResolveEntityIdAsync();
        await RequirePermissionAsync(entityId, Permissions.Transactions.Write);
        await _qifImportService.DeleteImportFileAsync(fileId, entityId);
        return NoContent();
    }
}
