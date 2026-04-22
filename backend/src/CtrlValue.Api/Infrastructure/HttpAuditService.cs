using Microsoft.Extensions.Logging;
using CtrlValue.Application.Interfaces;
using CtrlValue.Application.Services;
using CtrlValue.Infrastructure.Data;

namespace CtrlValue.Api.Infrastructure;

/// <summary>
/// Wraps AuditService to automatically extract IP address and User-Agent from the
/// current HTTP context before delegating to the base audit writer.
/// Registered in DI instead of AuditService directly.
/// </summary>
public class HttpAuditService : IAuditService
{
    private readonly AuditService _inner;
    private readonly IHttpContextAccessor _http;

    public HttpAuditService(AppDbContext db, ILogger<AuditService> logger, IHttpContextAccessor http)
    {
        _inner = new AuditService(db, logger);
        _http  = http;
    }

    public Task LogAsync(
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
        var ctx = _http.HttpContext;
        var ip  = ipAddress ?? ctx?.Connection.RemoteIpAddress?.ToString();
        var ua  = userAgent ?? ctx?.Request.Headers["User-Agent"].FirstOrDefault();

        return _inner.LogAsync(action, tenantId, userId, entityId, objectType, objectId, detail, ip, ua);
    }
}
