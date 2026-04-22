using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain;

namespace CtrlValue.Api.Controllers;

[Route("api/[controller]")]
[Authorize]
public class TransactionsController : EntityContextController
{
    private readonly ITransactionService _transactionService;

    public TransactionsController(ITransactionService transactionService, IEntityService entityService, IPermissionService permissions)
        : base(entityService, permissions)
    {
        _transactionService = transactionService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<TransactionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TransactionDto>>> GetTransactions(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var entityId = await ResolveEntityIdAsync();
        var transactions = await _transactionService.GetTransactionsAsync(entityId, startDate, endDate);
        return Ok(transactions);
    }

    [HttpGet("by-account/{accountId}")]
    [ProducesResponseType(typeof(List<TransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<TransactionDto>>> GetTransactionsByAccount(Guid accountId)
    {
        var entityId = await ResolveEntityIdAsync();
        var transactions = await _transactionService.GetTransactionsByAccountAsync(accountId, entityId);
        return Ok(transactions);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(TransactionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TransactionDto>> GetTransaction(Guid id)
    {
        var entityId = await ResolveEntityIdAsync();
        var transaction = await _transactionService.GetTransactionByIdAsync(id, entityId);
        return Ok(transaction);
    }

    [HttpPost]
    [ProducesResponseType(typeof(TransactionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TransactionDto>> CreateTransaction([FromBody] CreateTransactionRequest request)
    {
        var entityId = await ResolveEntityIdAsync();
        await RequirePermissionAsync(entityId, Permissions.Transactions.Write);
        var transaction = await _transactionService.CreateTransactionAsync(request, entityId);
        return CreatedAtAction(nameof(GetTransaction), new { id = transaction.Id }, transaction);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(TransactionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TransactionDto>> UpdateTransaction(Guid id, [FromBody] UpdateTransactionRequest request)
    {
        var entityId = await ResolveEntityIdAsync();
        await RequirePermissionAsync(entityId, Permissions.Transactions.Write);
        var transaction = await _transactionService.UpdateTransactionAsync(id, request, entityId);
        return Ok(transaction);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTransaction(Guid id)
    {
        var entityId = await ResolveEntityIdAsync();
        await RequirePermissionAsync(entityId, Permissions.Transactions.Write);
        await _transactionService.DeleteTransactionAsync(id, entityId);
        return NoContent();
    }

    [HttpPost("bulk-delete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> BulkDeleteTransactions([FromBody] BulkDeleteTransactionsRequest request)
    {
        if (request == null || request.TransactionIds == null || request.TransactionIds.Count == 0)
            return BadRequest("At least one transaction ID must be provided.");

        var entityId = await ResolveEntityIdAsync();
        await RequirePermissionAsync(entityId, Permissions.Transactions.Write);
        await _transactionService.BulkDeleteTransactionsAsync(request.TransactionIds, entityId);

        return NoContent();
    }
}
