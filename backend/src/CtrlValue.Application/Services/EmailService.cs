using System.Net;
using System.Net.Mail;
using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Configuration;
using CtrlValue.Application.Interfaces;

namespace CtrlValue.Application.Services;

/// <summary>
/// Sends transactional emails.
/// In production (when Email:AcsConnectionString is set): uses Azure Communication Services SDK.
/// In development (Mailpit): falls back to plain SMTP via System.Net.Mail.
/// </summary>
public class EmailService : IEmailService
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config)
    {
        _config = config;
    }

    // ── Public methods ────────────────────────────────────────────────────────

    public async Task SendEmailVerificationAsync(string toEmail, string firstName, string verificationToken, string tenantId)
    {
        var template = _config["Email:FrontendBaseUrl"] ?? "http://localhost:4200";
        var baseUrl  = template.Replace("{tenant}", tenantId);
        var link     = $"{baseUrl}/verify-email?token={verificationToken}";

        var subject = "Verify your Project Z email";
        var body    = $"""
            <h2>Hi {firstName},</h2>
            <p>Thanks for registering. Please verify your email address to activate your account.</p>
            <p><a href="{link}" style="padding:10px 20px;background:#4f46e5;color:white;border-radius:6px;text-decoration:none;">Verify Email</a></p>
            <p>Or copy and paste: <a href="{link}">{link}</a></p>
            <p>This link expires in <strong>24 hours</strong>.</p>
            <p>If you didn't register for Project Z, you can safely ignore this email.</p>
            """;

        await SendAsync(toEmail, subject, body);
    }

    public async Task SendPasswordResetAsync(string toEmail, string firstName, string resetToken)
    {
        var baseUrl = _config["Email:PasswordResetBaseUrl"] ?? "http://localhost:4200/reset-password";
        var link    = $"{baseUrl}?token={resetToken}";

        var subject = "Reset your Project Z password";
        var body    = $"""
            <h2>Hi {firstName},</h2>
            <p>We received a request to reset your password.</p>
            <p><a href="{link}" style="padding:10px 20px;background:#4f46e5;color:white;border-radius:6px;text-decoration:none;">Reset Password</a></p>
            <p>This link expires in <strong>1 hour</strong>.</p>
            <p>If you didn't request a password reset, you can safely ignore this email.</p>
            """;

        await SendAsync(toEmail, subject, body);
    }

    public async Task SendInviteEmailAsync(string toEmail, string inviteToken)
    {
        var baseUrl = _config["Email:InviteBaseUrl"] ?? "http://localhost:4200/register";
        var link    = $"{baseUrl}?invite={inviteToken}";

        var subject = "You've been invited to Project Z";
        var body    = $"""
            <h2>You're invited!</h2>
            <p>A workspace has been shared with you on Project Z.</p>
            <p><a href="{link}" style="padding:10px 20px;background:#4f46e5;color:white;border-radius:6px;text-decoration:none;">Accept Invitation</a></p>
            <p>Or copy and paste: <a href="{link}">{link}</a></p>
            <p>This link expires in <strong>48 hours</strong>.</p>
            """;

        await SendAsync(toEmail, subject, body);
    }

    // ── Routing ───────────────────────────────────────────────────────────────

    private Task SendAsync(string toEmail, string subject, string htmlBody)
    {
        var acsConnectionString = _config["Email:AcsConnectionString"];

        return string.IsNullOrWhiteSpace(acsConnectionString)
            ? SendViaSmtpAsync(toEmail, subject, htmlBody)     // Dev: Mailpit
            : SendViaAcsAsync(acsConnectionString, toEmail, subject, htmlBody);  // Prod: ACS SDK
    }

    // ── ACS SDK (production) ──────────────────────────────────────────────────

    private async Task SendViaAcsAsync(string connectionString, string toEmail, string subject, string htmlBody)
    {
        var fromAddress = _config["Email:FromAddress"] ?? "DoNotReply@yourdomain.azurecomm.net";

        var client  = new EmailClient(connectionString);
        var message = new EmailMessage(
            senderAddress: fromAddress,
            content:       new EmailContent(subject) { Html = htmlBody },
            recipients:    new EmailRecipients(new List<EmailAddress> { new EmailAddress(toEmail) })
        );

        // WaitUntil.Started returns immediately; use .Completed to wait for delivery confirmation
        await client.SendAsync(WaitUntil.Started, message);
    }

    // ── SMTP fallback (dev / Mailpit) ─────────────────────────────────────────

    private async Task SendViaSmtpAsync(string toEmail, string subject, string htmlBody)
    {
        var host        = _config["Email:SmtpHost"]    ?? "localhost";
        var port        = int.TryParse(_config["Email:SmtpPort"], out var p) ? p : 1025;
        var fromAddress = _config["Email:FromAddress"] ?? "noreply@CtrlValue.local";
        var fromName    = _config["Email:FromName"]    ?? "Project Z";

        using var client = new SmtpClient(host, port)
        {
            EnableSsl   = false,
            Credentials = CredentialCache.DefaultNetworkCredentials
        };

        var message = new MailMessage
        {
            From       = new MailAddress(fromAddress, fromName),
            Subject    = subject,
            Body       = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(toEmail);

        await client.SendMailAsync(message);
    }
}
