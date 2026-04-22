using Microsoft.Extensions.Logging;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain.Entities;
using CtrlValue.Infrastructure.Data;

namespace CtrlValue.Application.Services;

public class AuditService : IAuditService
{
    private readonly AppDbContext _db;
    private readonly ILogger<AuditService> _logger;

    public AuditService(AppDbContext db, ILogger<AuditService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task LogAsync(
        string action,
        string tenantId = "system",
        Guid? userId = null,
        Guid? entityId = null,
        string? objectType = null,
        string? objectId = null,
        string? detail = null,
        string? ipAddress = null,
        string? userAgent = null)
    {
        try
        {
            var log = new AuditLog
            {
                TenantId   = tenantId,
                UserId     = userId ?? Guid.Empty,
                EntityId   = entityId,
                Action     = action,
                ObjectType = objectType,
                ObjectId   = objectId,
                Detail     = detail,
                IpAddress  = ipAddress,
                UserAgent  = userAgent,
                Timestamp  = DateTime.UtcNow
            };

            _db.AuditLogs.Add(log);
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write audit log for action {Action} tenant {TenantId}", action, tenantId);
        }
    }
}
