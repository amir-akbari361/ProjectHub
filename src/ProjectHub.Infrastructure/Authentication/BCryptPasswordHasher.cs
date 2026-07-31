using ProjectHub.Application.Abstractions.Authentication;

namespace ProjectHub.Infrastructure.Authentication;

/// <summary>
/// BCrypt adapter for <see cref="IPasswordHasher"/>. BCrypt is the industry-standard choice for
/// password hashing because it's deliberately slow (adjustable work factor resists brute-force) and
/// salted (each hash includes a unique random salt, so identical passwords yield different hashes).
/// This prevents rainbow-table attacks and makes parallelized cracking economically infeasible.
/// </summary>
/// <remarks>
/// Why BCrypt over PBKDF2 or Argon2? BCrypt is mature (20+ years in production), universally
/// supported, and has a proven track record. The work factor (cost) can be tuned as hardware improves
/// without changing the stored hash format — when you verify an old hash, BCrypt reads its embedded
/// cost and applies it; when you hash a new password, you use the current configured cost. This
/// adapter uses cost 12 (4096 rounds), which is ~250ms on modern hardware — slow enough to deter
/// attacks but fast enough for login UX. The BCrypt.Net-Next library is battle-tested and actively
/// maintained; it's the de facto .NET implementation.
/// </remarks>
internal sealed class BCryptPasswordHasher : IPasswordHasher
{
    /// <summary>
    /// The computational cost factor (log₂ of iteration count). Cost 12 means 2^12 = 4096 rounds of
    /// the Blowfish key-expansion algorithm. Each increment doubles the work. OWASP recommends 12-14
    /// as of 2025; we pick 12 for a balance between security and user experience. If threat models
    /// evolve, bump this to 13 or 14 — existing hashes stay valid because the cost is embedded.
    /// </summary>
    private const int WorkFactor = 12;

    /// <summary>
    /// Hashes a plaintext password using BCrypt with an auto-generated salt. The resulting string is
    /// a self-contained 60-character encoded value: `$2a$12$[22-char-salt][31-char-hash]`. The `$2a$`
    /// prefix identifies the BCrypt variant; `12` is the work factor; the salt and hash are base-64
    /// encoded. This format is portable across implementations and safe to store in a VARCHAR(60).
    /// </summary>
    /// <param name="password">The plaintext password to hash. Never logged or persisted.</param>
    /// <returns>
    /// The complete BCrypt hash string. Store this in the database; discard the raw password immediately.
    /// </returns>
    public string Hash(string password)
    {
        // BCrypt.Net's HashPassword generates a random salt internally using RNGCryptoServiceProvider
        // (a CSPRNG), so we don't need to manage salts ourselves. The returned string embeds both
        // the salt and the hash in a single value, which is why BCrypt is so ergonomic to use.
        return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
    }

    /// <summary>
    /// Verifies a candidate password against a stored BCrypt hash. This is a **constant-time**
    /// comparison: BCrypt hashes the candidate with the embedded salt and compares the result to the
    /// stored hash in a way that doesn't leak timing information. Returning false (rather than throwing)
    /// lets the handler respond with a generic "invalid credentials" error without revealing whether
    /// the email or password was wrong — this prevents account enumeration via timing side-channels.
    /// </summary>
    /// <param name="password">The candidate password from the login request.</param>
    /// <param name="passwordHash">The stored BCrypt hash from the database (User.PasswordHash).</param>
    /// <returns>
    /// True if the candidate matches; false if it doesn't, the hash is malformed, or verification fails
    /// for any reason. The Application layer treats false as "invalid credentials" and never logs the
    /// raw password — only the user ID and a generic failure message.
    /// </returns>
    public bool Verify(string password, string passwordHash)
    {
        // BCrypt.Net's Verify reads the work factor and salt from the stored hash, applies them to
        // the candidate, and performs a constant-time comparison of the resulting hash. If the stored
        // hash is corrupt or uses an unsupported format, Verify returns false (never throws), so the
        // handler can safely treat all failures as "wrong password" without distinguishing error types.
        return BCrypt.Net.BCrypt.Verify(password, passwordHash);
    }
}
