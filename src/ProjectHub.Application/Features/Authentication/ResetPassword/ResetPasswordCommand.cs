using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Common;

namespace ProjectHub.Application.Features.Authentication.ResetPassword;

/// <summary>
/// Command that COMPLETES the password-reset flow: the user presents the one-time token from the
/// emailed link plus their chosen new password. Returns a plain <see cref="Result"/> — success means
/// the password was changed and all sessions revoked. We take only the raw token, never a user id;
/// possession of the token proves the caller controls the mailbox, so we resolve the owner server-side.
/// </summary>
/// <param name="Token">The raw reset token from the emailed link.</param>
/// <param name="NewPassword">The new plaintext password (hashed server-side; never stored raw).</param>
public sealed record ResetPasswordCommand(string Token, string NewPassword) : ICommand;
