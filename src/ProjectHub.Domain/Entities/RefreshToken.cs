using ProjectHub.Domain.Common;
using ProjectHub.Domain.Primitives;

namespace ProjectHub.Domain.Entities;

/// <summary>
/// A single refresh-token grant belonging to a <see cref="User"/>. It is a CHILD entity of the User
/// aggregate — never a root — so it is created, rotated, and revoked only through User methods and is
/// reached only via <see cref="User.RefreshTokens"/>. We store a SHA-256 <b>hash</b> of the raw token
/// (never the raw value): if the database leaks, an attacker still cannot present a valid token,
/// exactly as we hash passwords rather than storing them in the clear.
/// </summary>
public sealed class RefreshToken : Entity
{
    // Internal so only the User aggregate (same assembly) can construct one. This enforces the
    // aggregate boundary at compile time: application code cannot new-up a token behind User's back.
    internal RefreshToken(Guid userId, string tokenHash, DateTime expiresAtUtc, DateTime createdAtUtc)
        : base(Guid.NewGuid())
    {
        UserId = Guard.NotEmpty(userId, nameof(userId));
        TokenHash = Guard.NotNullOrWhiteSpace(tokenHash, nameof(tokenHash));
        ExpiresAtUtc = expiresAtUtc;
        MarkCreated(createdAtUtc);
    }

    // Parameterless ctor exists ONLY for EF Core materialisation. It is private so no domain code
    // can build an invalid, field-less token; EF uses reflection to bypass the accessibility.
    private RefreshToken()
        : base(Guid.Empty)
    {
        TokenHash = null!;
    }

    public Guid UserId { get; private set; }

    // The SHA-256 hash of the opaque random token handed to the client — the secret at rest.
    public string TokenHash { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }

    // Set when the token is explicitly revoked (logout) OR consumed during rotation. A non-null
    // value means "no longer usable"; the timestamp gives us an audit trail of when it happened.
    public DateTime? RevokedAtUtc { get; private set; }

    // Rotation breadcrumb: when this token is exchanged for a new one, we record the successor's
    // hash. If a client ever presents a token that is revoked AND has a successor, that is a replay
    // of an already-rotated token — a theft signal we can act on (revoke the whole chain).
    public string? ReplacedByTokenHash { get; private set; }

    // Derived, never stored: a token is usable only while it is neither revoked nor past expiry.
    public bool IsActive(DateTime utcNow) => RevokedAtUtc is null && utcNow < ExpiresAtUtc;

    // Called by the User aggregate on logout or during rotation. Idempotent-safe callers guard first.
    internal void Revoke(DateTime utcNow, string? replacedByTokenHash = null)
    {
        RevokedAtUtc = utcNow;
        ReplacedByTokenHash = replacedByTokenHash;
        MarkUpdated(utcNow);
    }
}
