using ProjectHub.Domain.Abstractions;

namespace ProjectHub.Domain.Events;

/// <summary>
/// Raised when a user completes a password reset via a reset link. A downstream handler will typically
/// revoke every active refresh token for the user (log out all sessions) and send a security-notice
/// email ("your password was changed") — both critical reactions to a credential change, kept out of
/// the aggregate so the domain stays free of email/session concerns.
/// </summary>
public sealed record UserPasswordResetDomainEvent(Guid UserId, string Email, DateTime OccurredAtUtc)
    : IDomainEvent;
