namespace CtrlValue.Application.Interfaces;

public interface IAuditService
{
    /// <summary>
    /// Write an audit log entry. Never throws — failures are swallowed and logged via ILogger
    /// so that a failed audit write never breaks the calling operation.
    /// </summary>
    Task LogAsync(
        string action,
        string tenantId = "system",
        Guid? userId = null,
        Guid? entityId = null,
        string? objectType = null,
        string? objectId = null,
        string? detail = null,
        string? ipAddress = null,
        string? userAgent = null);
}
