using ProjectHub.Domain.Abstractions;
using ProjectHub.Domain.Enums;

namespace ProjectHub.Domain.Events;

public sealed record NotificationCreatedDomainEvent(
    Guid NotificationId,
    Guid RecipientId,
    NotificationType Type,
    DateTime OccurredAtUtc) : IDomainEvent;
