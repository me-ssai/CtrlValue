namespace CtrlValue.Application.Interfaces;

public interface IUserDeletionService
{
    /// <summary>
    /// Permanently hard-deletes all data owned by the user, then removes the user record.
    /// Logs an audit event before deletion. Call this from both the background scheduler
    /// (30-day expiry) and the expedited-deletion approval flow.
    /// </summary>
    Task ExecuteUserDeletionAsync(Guid userId, Guid actingUserId);
}
