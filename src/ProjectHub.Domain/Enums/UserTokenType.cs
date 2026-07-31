namespace ProjectHub.Domain.Enums;

/// <summary>
/// Discriminates the PURPOSE of a one-time <see cref="Entities.UserToken"/>. A single token table
/// backs several flows (email confirmation, password reset), and this enum prevents a token minted
/// for one purpose from being redeemed for another — a "confusion" attack where, e.g., a leaked
/// email-confirmation link is replayed against the password-reset endpoint. The redemption code
/// always checks BOTH the hash AND that the type matches the operation being performed.
/// </summary>
public enum UserTokenType
{
    /// <summary>Proves the user controls the email address they registered with.</summary>
    EmailConfirmation = 1,

    /// <summary>Authorizes a single password reset without the current password.</summary>
    PasswordReset = 2,
}
