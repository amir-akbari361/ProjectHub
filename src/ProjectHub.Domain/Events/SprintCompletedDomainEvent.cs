using ProjectHub.Domain.Abstractions;

namespace ProjectHub.Domain.Events;

public sealed record SprintCompletedDomainEvent(
    Guid SprintId,
    Guid ProjectId,
    DateTime OccurredAtUtc) : IDomainEvent;
