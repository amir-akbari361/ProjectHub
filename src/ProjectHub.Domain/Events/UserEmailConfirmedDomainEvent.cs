using ProjectHub.Domain.Abstractions;

namespace ProjectHub.Domain.Events;

/// <summary>
/// Raised when a user successfully confirms their email address. Downstream handlers may, for example,
/// send a welcome email or unlock features that require a verified address — but those reactions live
/// OUTSIDE the aggregate, decoupled via this event.
/// </summary>
public sealed record UserEmailConfirmedDomainEvent(Guid UserId, string Email, DateTime OccurredAtUtc)
    : IDomainEvent;
