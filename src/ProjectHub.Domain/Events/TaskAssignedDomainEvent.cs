using ProjectHub.Domain.Abstractions;

namespace ProjectHub.Domain.Events;

public sealed record TaskAssignedDomainEvent(
    Guid TaskId,
    Guid ProjectId,
    Guid AssigneeId,
    DateTime OccurredAtUtc) : IDomainEvent;
