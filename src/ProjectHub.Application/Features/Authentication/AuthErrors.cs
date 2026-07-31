using ProjectHub.Application.Common;

namespace ProjectHub.Application.Features.Authentication;

/// <summary>
/// Centralized catalog of authentication errors as reusable <see cref="Error"/> values. Keeping
/// them here (rather than scattering magic strings across handlers) means the error CODES become a
/// stable contract the UI/API can switch on, and prevents subtle drift between similar messages.
/// </summary>
public static class AuthErrors
{
    /// <summary>
    /// Returned when registration hits an email that already exists. Modeled as a Conflict (409),
    /// not a Validation error, because the input was well-formed — it collided with server state.
    /// The message is deliberately generic to avoid confirming which emails are registered
    /// (account-enumeration hardening).
    /// </summary>
    public static readonly Error EmailAlreadyInUse = Error.Conflict(
        "Auth.EmailAlreadyInUse",
        "Registration could not be completed with the provided details.");

    /// <summary>
    /// Returned when login fails because the email is unknown OR the password is wrong. We use ONE
    /// error for both cases on purpose: distinguishing them would let an attacker enumerate which
    /// emails are registered. Modeled as Unauthorized (401) — the caller is not authenticated.
    /// </summary>
    public static readonly Error InvalidCredentials = Error.Unauthorized(
        "Auth.InvalidCredentials",
        "The email or password is incorrect.");

    /// <summary>
    /// Returned when credentials are valid but the account has been deactivated (admin suspension).
    /// Distinct from InvalidCredentials because the user proved their identity; they just aren't
    /// permitted to use the system right now. Modeled as Forbidden (403).
    /// </summary>
    public static readonly Error AccountInactive = Error.Forbidden(
        "Auth.AccountInactive",
        "This account is inactive. Please contact an administrator.");

    /// <summary>
    /// Returned when a refresh token is unknown, expired, or already rotated/revoked. Like login, we
    /// keep this generic so a probing client can't distinguish "never existed" from "already used".
    /// Modeled as Unauthorized (401) — the caller must re-authenticate with credentials.
    /// </summary>
    public static readonly Error InvalidRefreshToken = Error.Unauthorized(
        "Auth.InvalidRefreshToken",
        "The refresh token is invalid or has expired. Please sign in again.");

    /// <summary>
    /// Returned when an email-confirmation link is unknown, expired, already used, or the account is
    /// already confirmed. Kept generic (one code for every failure mode) so a probing client can't
    /// distinguish "wrong token" from "already confirmed". Modeled as Validation (400) — the request
    /// was syntactically fine but the token could not be redeemed.
    /// </summary>
    public static readonly Error InvalidEmailConfirmationToken = Error.Validation(
        "Auth.InvalidEmailConfirmationToken",
        "The confirmation link is invalid or has expired.");

    /// <summary>
    /// Returned when a password-reset link is unknown, expired, or already used. Generic on purpose,
    /// exactly like the confirmation error. Modeled as Validation (400).
    /// </summary>
    public static readonly Error InvalidPasswordResetToken = Error.Validation(
        "Auth.InvalidPasswordResetToken",
        "The password reset link is invalid or has expired.");
}

