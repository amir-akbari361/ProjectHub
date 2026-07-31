namespace ProjectHub.Application.Abstractions.Authentication;

/// <summary>
/// Port for delivering transactional auth emails (confirmation links, password-reset links).
/// The Application layer only knows "send this recipient this link" — it is deliberately ignorant
/// of SMTP, SendGrid, templating, or retry policy, all of which live behind the Infrastructure
/// adapter. Every method is async because network I/O must never block a request thread, and each
/// accepts a <see cref="CancellationToken"/> so a cancelled HTTP request abandons the send.
/// </summary>
public interface IEmailSender
{
    /// <summary>Sends an account-activation email containing a one-time confirmation link.</summary>
    Task SendEmailConfirmationAsync(string recipientEmail, string confirmationLink, CancellationToken cancellationToken = default);

    /// <summary>Sends a password-reset email containing a time-limited, one-time reset link.</summary>
    Task SendPasswordResetAsync(string recipientEmail, string resetLink, CancellationToken cancellationToken = default);
}
