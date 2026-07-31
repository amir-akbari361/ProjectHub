using System.Security.Cryptography;
using ProjectHub.Application.Abstractions.Authentication;

namespace ProjectHub.Infrastructure.Authentication;

/// <summary>
/// CSPRNG adapter for <see cref="ISecureTokenGenerator"/>. Generates cryptographically-random opaque
/// tokens for refresh tokens and email-confirmation / password-reset links. These tokens must be
/// unpredictable — <c>Guid.NewGuid()</c> and <c>Random</c> are both INSUFFICIENT for security material
/// because they're seeded predictably and can be brute-forced. This adapter uses .NET's
/// <c>RandomNumberGenerator</c>, which is a CSPRNG (Cryptographically Secure Pseudo-Random Number
/// Generator) backed by the OS's entropy pool (CryptGenRandom on Windows, /dev/urandom on Linux).
/// </summary>
/// <remarks>
/// Token format: we generate 32 random bytes (256 bits of entropy — the same as a SHA-256 hash) and
/// encode them as URL-safe base64 (no `+`, `/`, or `=` padding that would break query strings). This
/// yields a ~43-character string that's compact, collision-free, and safe to embed in URLs or JSON.
/// The high entropy makes guessing infeasible: 2^256 possible values means an attacker would need to
/// try quintillions of tokens to find one valid refresh token in the database, which is economically
/// impossible even at scale. We pair this with token expiry and single-use semantics (refresh rotation)
/// to further limit the attack window — a stolen token becomes useless after one refresh or 7 days.
/// </remarks>
internal sealed class SecureTokenGenerator : ISecureTokenGenerator
{
    /// <summary>
    /// Number of random bytes to generate. 32 bytes = 256 bits, which matches the security level of
    /// SHA-256 (our hashing function) and is the recommended minimum for bearer tokens per NIST and
    /// OWASP. More bytes would make the base64 encoding longer without adding meaningful security;
    /// fewer bytes would reduce the brute-force cost below acceptable thresholds.
    /// </summary>
    private const int TokenLengthBytes = 32;

    /// <summary>
    /// Generates a cryptographically-random, URL-safe token. This is the raw value shown exactly once
    /// to the client; only its SHA-256 hash is persisted (see <see cref="Sha256TokenHasher"/>).
    /// </summary>
    /// <returns>
    /// A 43-character URL-safe base64 string. No padding (`=`) appears because we strip it — the length
    /// is deterministic, so the client (or our own hash function) can decode without it. The string is
    /// safe to embed in URLs, JSON, and HTTP headers without escaping.
    /// </returns>
    public string GenerateToken()
    {
        // Allocate a buffer for the random bytes. We use a stack-allocated Span here for zero-heap
        // allocation — the buffer is small (32 bytes) and short-lived, so stack is ideal.
        Span<byte> randomBytes = stackalloc byte[TokenLengthBytes];

        // Fill the buffer with cryptographically-random bytes. RandomNumberGenerator.Fill uses the OS
        // CSPRNG under the hood, which pools entropy from hardware noise, keyboard timing, network
        // jitter, etc. This is unpredictable even to an attacker who controls the application process.
        RandomNumberGenerator.Fill(randomBytes);

        // Encode as URL-safe base64. Standard base64 uses `+` and `/` (problematic in URLs) and appends
        // `=` padding. Convert.ToBase64String with these options replaces `+` → `-`, `/` → `_`, and
        // strips padding. The result is deterministic (same bytes → same string) and reversible (the
        // hash function can decode it), but we never need to decode — we only hash and compare.
        return Convert.ToBase64String(
            randomBytes,
            Base64FormattingOptions.None)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
