namespace ProjectHub.Application.Abstractions.Authentication;

/// <summary>
/// Port for generating cryptographically-random opaque tokens. Used for refresh tokens and for
/// email-confirmation / password-reset links. The Application layer must never use
/// <c>Guid.NewGuid()</c> or <c>Random</c> for security material — both are predictable. The
/// Infrastructure adapter uses a CSPRNG (<c>RandomNumberGenerator</c>) with sufficient entropy.
/// </summary>
public interface ISecureTokenGenerator
{
    /// <summary>
    /// Produces a high-entropy, URL-safe random string. The raw value is returned exactly once to
    /// the caller; only its hash is ever persisted (see <see cref="ITokenHasher"/>).
    /// </summary>
    string GenerateToken();
}
