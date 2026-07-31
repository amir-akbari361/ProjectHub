using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Common;

namespace ProjectHub.Application.Features.Authentication.ForgotPassword;

/// <summary>
/// Command that STARTS the password-reset flow: the user supplies their email, and if it maps to an
/// account we mint a one-time reset token and email a reset link. Returns a plain <see cref="Result"/>
/// with no payload — and, critically, ALWAYS succeeds regardless of whether the email exists. Telling
/// the caller "no such account" would turn this endpoint into an email-enumeration oracle, so the
/// response is intentionally indistinguishable in both cases.
/// </summary>
/// <param name="Email">The email address to send a reset link to (if it belongs to an account).</param>
public sealed record ForgotPasswordCommand(string Email) : ICommand;
