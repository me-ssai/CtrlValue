using CtrlValue.Application.DTOs;
using CtrlValue.Domain.Entities;

namespace CtrlValue.Application.Interfaces;

public interface IAgentAuditService
{
    /// <summary>Appends an audit log entry. Fire-and-forget safe — never throws.</summary>
    Task RecordAsync(AgentAuditLog entry);

    Task<List<AgentAuditLogDto>> GetAuditLogsAsync(
        Guid? userId = null,
        int page = 1,
        int pageSize = 50);
}
