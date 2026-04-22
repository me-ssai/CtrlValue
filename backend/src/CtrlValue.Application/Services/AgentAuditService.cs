using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain.Entities;
using CtrlValue.Infrastructure.Data;

namespace CtrlValue.Application.Services;

public class AgentAuditService : IAgentAuditService
{
    private readonly AppDbContext _db;
    private readonly ILogger<AgentAuditService> _logger;

    public AgentAuditService(AppDbContext db, ILogger<AgentAuditService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task RecordAsync(AgentAuditLog entry)
    {
        try
        {
            _db.AgentAuditLogs.Add(entry);
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Audit log failure must never crash the main request
            _logger.LogError(ex, "[AgentAudit] Failed to write audit log for UserId={UserId} RequestType={Type}",
                entry.UserId, entry.RequestType);
        }
    }

    public async Task<List<AgentAuditLogDto>> GetAuditLogsAsync(
        Guid? userId = null,
        int page = 1,
        int pageSize = 50)
    {
        var query = _db.AgentAuditLogs.AsNoTracking();

        if (userId.HasValue)
            query = query.Where(a => a.UserId == userId.Value);

        var logs = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return logs.Select(a => new AgentAuditLogDto
        {
            Id = a.Id,
            ConversationId = a.ConversationId,
            UserId = a.UserId,
            RequestType = a.RequestType,
            Provider = a.Provider,
            Model = a.Model,
            SafetyDecision = a.SafetyDecision,
            TotalTokens = a.TotalTokens,
            CreatedAt = a.CreatedAt
        }).ToList();
    }
}
