using CtrlValue.Application.DTOs;

namespace CtrlValue.Application.Interfaces;

public interface IAgentSavingsHistoryService
{
    /// <summary>
    /// Returns savings rate snapshots for the entity, newest first.
    /// </summary>
    Task<List<SavingsSnapshotDto>> GetHistoryAsync(Guid entityId, int months = 24);

    /// <summary>
    /// Records a snapshot for the current month from the live finance context.
    /// Upserts — if a snapshot for this month already exists it is updated.
    /// Called automatically after each context refresh.
    /// </summary>
    Task RecordSnapshotAsync(Guid userId, Guid entityId, FinanceContextDto ctx, CancellationToken ct = default);
}
