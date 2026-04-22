using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain;

namespace CtrlValue.Api.Controllers;

/// <summary>Request model for OFX file upload.</summary>
public class OfxUploadRequest
{
    /// <summary>The .ofx file to import.</summary>
    public IFormFile File { get; set; } = null!;
    /// <summary>ID of the account to import transactions into.</summary>
    public Guid AccountId { get; set; }
    /// <summary>If true, transactions with matching FITIDs will still be imported.</summary>
    public bool AllowDuplicates { get; set; } = false;
}

[ApiController]
[Route("api/ofx-import")]
[Authorize]
public class OfxImportController : EntityContextController
{
    private readonly IOfxImportService _ofxImportService;

    public OfxImportController(IOfxImportService ofxImportService, IEntityService entityService, IPermissionService permissions)
        : base(entityService, permissions)
    {
        _ofxImportService = ofxImportService;
    }

    /// <summary>Upload a .ofx file, parse it, and stage all transactions for review.</summary>
    [HttpPost("upload")]
    [RequestSizeLimit(10_485_760)] // 10 MB
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(OfxImportedTransactionsFileDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OfxImportedTransactionsFileDto>> Upload([FromForm] OfxUploadRequest request)
    {
        if (request.File == null || request.File.Length == 0)
            return BadRequest(new { error = "No file provided." });

        var extension = Path.GetExtension(request.File.FileName).ToLowerInvariant();
        if (extension != ".ofx")
            return BadRequest(new { error = "Only .ofx files are supported." });

        var entityId = await ResolveEntityIdAsync();
        await RequirePermissionAsync(entityId, Permissions.Transactions.Write);

        await using var stream = request.File.OpenReadStream();
        var result = await _ofxImportService.UploadAndStageAsync(
            entityId,
            request.AccountId,
            request.AllowDuplicates,
            stream,
            request.File.FileName);

        return CreatedAtAction(nameof(GetStagedImport), new { fileId = result.Id }, result);
    }

    /// <summary>List all OFX import files for the current entity.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<OfxImportedTransactionsFileDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<OfxImportedTransactionsFileDto>>> GetImportFiles()
    {
        var entityId = await ResolveEntityIdAsync();
        var files = await _ofxImportService.GetImportFilesAsync(entityId);
        return Ok(files);
    }

    /// <summary>Get the staged import review data (rows grouped by status).</summary>
    [HttpGet("{fileId:guid}/staged")]
    [ProducesResponseType(typeof(OfxStagedImportReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OfxStagedImportReviewDto>> GetStagedImport(Guid fileId)
    {
        var entityId = await ResolveEntityIdAsync();
        var review = await _ofxImportService.GetStagedImportAsync(fileId, entityId);
        return Ok(review);
    }

    /// <summary>Update a single staging row's account and/or category selection.</summary>
    [HttpPut("{fileId:guid}/staging/{rowId:guid}")]
    [ProducesResponseType(typeof(OfxStagingRowDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OfxStagingRowDto>> UpdateStagingRow(
        Guid fileId,
        Guid rowId,
        [FromBody] UpdateStagingRowRequest request)
    {
        var entityId = await ResolveEntityIdAsync();
        await RequirePermissionAsync(entityId, Permissions.Transactions.Write);
        var row = await _ofxImportService.UpdateStagingRowAsync(fileId, rowId, request, entityId);
        return Ok(row);
    }

    /// <summary>Commit all valid staging rows into the transactions table.</summary>
    [HttpPost("{fileId:guid}/commit")]
    [ProducesResponseType(typeof(OfxImportedTransactionsFileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OfxImportedTransactionsFileDto>> CommitImport(Guid fileId)
    {
        var entityId = await ResolveEntityIdAsync();
        await RequirePermissionAsync(entityId, Permissions.Transactions.Write);
        var result = await _ofxImportService.CommitImportAsync(fileId, entityId);
        return Ok(result);
    }

    /// <summary>Soft-delete an OFX import file and all its staging rows.</summary>
    [HttpDelete("{fileId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteImportFile(Guid fileId)
    {
        var entityId = await ResolveEntityIdAsync();
        await RequirePermissionAsync(entityId, Permissions.Transactions.Write);
        await _ofxImportService.DeleteImportFileAsync(fileId, entityId);
        return NoContent();
    }
}
