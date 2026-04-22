using CtrlValue.Application.DTOs;

namespace CtrlValue.Application.Interfaces;

public interface IDashboardService
{
    Task<DashboardSummary> GetDashboardSummaryAsync(Guid entityId);
}
