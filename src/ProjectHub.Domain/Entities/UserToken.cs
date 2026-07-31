using ProjectHub.Domain.Common;
using ProjectHub.Domain.Enums;
using ProjectHub.Domain.Primitives;

namespace ProjectHub.Domain.Entities;

/// <summary>
/// A single-use, purpose-scoped, time-limited secret belonging to a <see cref="User"/> — the backing
/// store for email-confirmation and password-reset links. Like <see cref="RefreshToken"/>, it is a
/// CHILD entity of the User aggregate (created and consumed only through User methods) and stores a
/// SHA-256 <b>hash</b>, never the raw token. The raw value travels once, inside the emailed link;
/// if our DB leaks, the hashes are useless to an attacker.
///
/// Three properties make it safe: <see cref="Type"/> stops cross-purpose replay, <see cref="ExpiresAtUtc"/>
/// bounds the theft window, and <see cref="ConsumedAtUtc"/> enforces single use.
/// </summary>
public sealed class UserToken : Entity
{
    // Internal: only the User aggregate (same assembly) may mint a token, preserving the boundary.
    internal UserToken(
        Guid userId,
        string tokenHash,
        UserTokenType type,
        DateTime expiresAtUtc,
        DateTime createdAtUtc)
        : base(Guid.NewGuid())
    {
        UserId = Guard.NotEmpty(userId, nameof(userId));
        TokenHash = Guard.NotNullOrWhiteSpace(tokenHash, nameof(tokenHash));
        Type = type;
        ExpiresAtUtc = expiresAtUtc;
        MarkCreated(createdAtUtc);
    }

    // EF Core materialisation only.
    private UserToken()
        : base(Guid.Empty)
    {
        TokenHash = null!;
    }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; }

    public UserTokenType Type { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }

    // Non-null once redeemed. Enforces single-use: a consumed token can never be redeemed again.
    public DateTime? ConsumedAtUtc { get; private set; }

    // Usable only while unconsumed and unexpired.
    public bool IsRedeemable(DateTime utcNow) => ConsumedAtUtc is null && utcNow < ExpiresAtUtc;

    // Called by the User aggregate when the link is successfully redeemed.
    internal void Consume(DateTime utcNow)
    {
        ConsumedAtUtc = utcNow;
        MarkUpdated(utcNow);
    }
}
