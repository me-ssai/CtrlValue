using CtrlValue.Application.DTOs;

namespace CtrlValue.Application.Interfaces;

public interface ITransactionService
{
    Task<List<TransactionDto>> GetTransactionsAsync(Guid entityId, DateTime? startDate = null, DateTime? endDate = null);
    Task<List<TransactionDto>> GetTransactionsByAccountAsync(Guid accountId, Guid entityId);
    Task<TransactionDto> GetTransactionByIdAsync(Guid id, Guid entityId);
    Task<TransactionDto> CreateTransactionAsync(CreateTransactionRequest request, Guid entityId);
    Task<TransactionDto> UpdateTransactionAsync(Guid id, UpdateTransactionRequest request, Guid entityId);
    Task DeleteTransactionAsync(Guid id, Guid entityId);
    Task BulkDeleteTransactionsAsync(List<Guid> transactionIds, Guid entityId);
}
