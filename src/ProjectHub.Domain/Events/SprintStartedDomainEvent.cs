using ProjectHub.Domain.Abstractions;

namespace ProjectHub.Domain.Events;

public sealed record SprintStartedDomainEvent(
    Guid SprintId,
    Guid ProjectId,
    DateTime OccurredAtUtc) : IDomainEvent;
