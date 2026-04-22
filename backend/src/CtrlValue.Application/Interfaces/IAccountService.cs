using CtrlValue.Application.DTOs;
using CtrlValue.Domain.Enums;

namespace CtrlValue.Application.Interfaces;

public interface IAccountService
{
    Task<List<AccountDto>> GetAccountsAsync(Guid entityId, AccountType? type = null);
    Task<AccountDto> GetAccountByIdAsync(Guid id, Guid entityId);
    Task<AccountDto> CreateAccountAsync(CreateAccountRequest request, Guid entityId);
    Task<AccountDto> UpdateAccountAsync(Guid id, UpdateAccountRequest request, Guid entityId);
    Task<AccountDeletionImpactDto> GetDeletionImpactAsync(Guid id, Guid entityId);
    Task DeleteAccountAsync(Guid id, Guid entityId);
    Task<AccountSummaryDto> GetAccountSummaryAsync(Guid entityId);
    Task RecalculateBalanceAsync(Guid accountId, Guid entityId);
    Task RecalculateAllBalancesAsync(Guid entityId);
}
