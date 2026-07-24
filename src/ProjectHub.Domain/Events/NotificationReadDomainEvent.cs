using ProjectHub.Domain.Abstractions;

namespace ProjectHub.Domain.Events;

public sealed record NotificationReadDomainEvent(
    Guid NotificationId,
    Guid RecipientId,
    DateTime OccurredAtUtc) : IDomainEvent;
