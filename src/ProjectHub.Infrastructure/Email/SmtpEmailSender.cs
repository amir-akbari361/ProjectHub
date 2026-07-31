using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectHub.Application.Abstractions.Authentication;

namespace ProjectHub.Infrastructure.Email;

/// <summary>
/// SMTP adapter for <see cref="IEmailSender"/>. Delivers transactional auth emails (confirmation and
/// password-reset links) over SMTP using <see cref="EmailOptions"/>. This is the only place that
/// touches an email-transport API — the Application layer only ever sees the port, so swapping SMTP
/// for a SendGrid/Postmark HTTP API later means changing just this file plus its registration.
/// </summary>
/// <remarks>
/// We use the framework's <see cref="SmtpClient"/> for zero external dependencies. It's marked
/// obsolete by Microsoft for advanced modern-protocol scenarios (OAuth2, etc.), and a production
/// system at scale would prefer MailKit; for our transactional STARTTLS use case it is entirely
/// adequate and keeps the dependency surface minimal. The two public methods differ only in subject
/// and body, so both delegate to a single private <c>SendAsync</c> — DRY, one place to evolve
/// retry/templating/logging.
/// </remarks>
internal sealed class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task SendEmailConfirmationAsync(
        string recipientEmail,
        string confirmationLink,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            recipientEmail,
            subject: "Confirm your ProjectHub account",
            htmlBody: $"""
                <p>Welcome to ProjectHub!</p>
                <p>Please confirm your account by clicking the link below:</p>
                <p><a href="{WebUtility.HtmlEncode(confirmationLink)}">Confirm my account</a></p>
                <p>If you didn't create this account, you can safely ignore this email.</p>
                """,
            cancellationToken);

    public Task SendPasswordResetAsync(
        string recipientEmail,
        string resetLink,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            recipientEmail,
            subject: "Reset your ProjectHub password",
            htmlBody: $"""
                <p>We received a request to reset your ProjectHub password.</p>
                <p>Click the link below to choose a new password. This link expires shortly:</p>
                <p><a href="{WebUtility.HtmlEncode(resetLink)}">Reset my password</a></p>
                <p>If you didn't request this, you can safely ignore this email — your password won't change.</p>
                """,
            cancellationToken);

    /// <summary>
    /// The single send path both public methods funnel through. Builds a fresh <see cref="SmtpClient"/>
    /// and <see cref="MailMessage"/> per call (both are cheap and NOT thread-safe, so per-call
    /// construction is the safe choice), sends the message, and logs the outcome. We deliberately let
    /// exceptions propagate: the calling command handler decides whether a delivery failure should fail
    /// the whole operation or be treated as best-effort — Infrastructure shouldn't swallow that decision.
    /// </summary>
    private async Task SendAsync(
        string recipientEmail,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken)
    {
        // MailMessage is IDisposable (it owns unmanaged handles for any attachments/streams); dispose it.
        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(new MailAddress(recipientEmail));

        // SmtpClient is also IDisposable and NOT reusable across concurrent sends, so it's scoped here.
        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.UseSsl,
            Credentials = new NetworkCredential(_options.Username, _options.Password)
        };

        // SendMailAsync honors the CancellationToken (.NET 6+ overload) so a cancelled request abandons
        // the send instead of blocking a thread on slow network I/O.
        await client.SendMailAsync(message, cancellationToken);

        // Structured log: we record the recipient and subject (both non-sensitive) for observability,
        // but never the link itself — a confirmation/reset link is a bearer secret and must not be logged.
        _logger.LogInformation(
            "Sent transactional email to {Recipient} with subject {Subject}",
            recipientEmail,
            subject);
    }
}
