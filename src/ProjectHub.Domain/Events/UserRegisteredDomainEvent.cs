using ProjectHub.Domain.Abstractions;

namespace ProjectHub.Domain.Events;

public sealed record UserRegisteredDomainEvent(
    Guid UserId,
    string Email,
    DateTime OccurredAtUtc) : IDomainEvent;
