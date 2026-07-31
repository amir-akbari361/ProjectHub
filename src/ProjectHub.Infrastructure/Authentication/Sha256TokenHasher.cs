using System.Security.Cryptography;
using System.Text;
using ProjectHub.Application.Abstractions.Authentication;

namespace ProjectHub.Infrastructure.Authentication;

/// <summary>
/// SHA-256 adapter for <see cref="ITokenHasher"/>. This hashes opaque, HIGH-ENTROPY security tokens
/// (refresh tokens, email-confirmation and password-reset links) before they touch the database.
/// </summary>
/// <remarks>
/// Why SHA-256 here and BCrypt for passwords? The two use cases have opposite requirements:
/// <list type="bullet">
///   <item>
///     Passwords are LOW-entropy (humans pick weak, guessable strings) so we need a DELIBERATELY
///     SLOW, SALTED hash (BCrypt) to make brute-forcing expensive. Each hash must be unique.
///   </item>
///   <item>
///     Our tokens are HIGH-entropy (256 bits of CSPRNG randomness — see <see cref="SecureTokenGenerator"/>)
///     so brute-forcing is already infeasible; we don't need slowness. What we DO need is DETERMINISM:
///     the same raw token must always produce the same hash, because we look tokens up BY their hash.
///     A salted hash would make lookup impossible (we'd have to try every salt). SHA-256 is fast,
///     unsalted, and deterministic — exactly right for this.
///   </item>
/// </list>
/// Security property: we persist only the hash. If the database leaks, an attacker gets hashes, not
/// the raw tokens the client presents — and SHA-256 is one-way, so they can't reverse them. Because
/// the tokens are 256-bit random, a precomputed rainbow table is infeasible, so the lack of salt is
/// acceptable HERE (it would NOT be acceptable for passwords).
/// </remarks>
internal sealed class Sha256TokenHasher : ITokenHasher
{
    /// <summary>
    /// Computes the deterministic SHA-256 hash of a raw token and returns it as a lowercase hex string.
    /// </summary>
    /// <param name="token">The raw, high-entropy token (never persisted in this form).</param>
    /// <returns>
    /// A 64-character lowercase hexadecimal string (256 bits / 4 bits-per-hex-char). This is what we
    /// store and what we query by, so the encoding must be stable — hex is deterministic and collision-free.
    /// </returns>
    public string Hash(string token)
    {
        // Encode the token as UTF-8 bytes. UTF-8 is deterministic for a given string, which preserves
        // the "same input → same hash" guarantee the interface requires.
        var bytes = Encoding.UTF8.GetBytes(token);

        // SHA256.HashData is a static, allocation-light one-shot API (no IDisposable instance needed).
        // It returns the 32-byte digest. SHA-256 is a fast cryptographic hash — perfect for a value we
        // must recompute on every token presentation to look up the stored record.
        var hash = SHA256.HashData(bytes);

        // Convert to lowercase hex. Convert.ToHexStringLower (.NET 9) is the fastest built-in and yields
        // a canonical, URL-safe, case-stable representation suitable for a VARCHAR index column.
        return Convert.ToHexStringLower(hash);
    }
}
