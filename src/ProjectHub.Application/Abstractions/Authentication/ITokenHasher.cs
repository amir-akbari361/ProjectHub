namespace ProjectHub.Application.Abstractions.Authentication;

/// <summary>
/// Port for deterministically hashing opaque security tokens (refresh tokens, reset links) before
/// they touch the database. Unlike <see cref="IPasswordHasher"/> — which is deliberately slow and
/// salted for low-entropy human passwords — this uses a fast, unsalted, deterministic hash (SHA-256)
/// so we can look a token up by its hash. Determinism is REQUIRED here: the same raw token must
/// always map to the same hash, otherwise a stored refresh token could never be found again.
/// </summary>
public interface ITokenHasher
{
    /// <summary>
    /// Returns the deterministic hash of a raw token. We persist only this hash; if the database
    /// leaks, the raw tokens (which are what the client presents) are not recoverable from it.
    /// </summary>
    string Hash(string token);
}
