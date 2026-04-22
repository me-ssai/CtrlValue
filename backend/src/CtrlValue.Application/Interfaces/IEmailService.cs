namespace CtrlValue.Application.Interfaces;

public interface IEmailService
{
    Task SendEmailVerificationAsync(string toEmail, string firstName, string verificationToken, string tenantId);
    Task SendPasswordResetAsync(string toEmail, string firstName, string resetToken);
    Task SendInviteEmailAsync(string toEmail, string inviteToken);
}
