using ProjectHub.Domain.Abstractions;
using ProjectHub.Domain.Enums;

namespace ProjectHub.Domain.Events;

public sealed record TaskStatusChangedDomainEvent(
    Guid TaskId,
    Guid ProjectId,
    ProjectTaskStatus OldStatus,
    ProjectTaskStatus NewStatus,
    DateTime OccurredAtUtc) : IDomainEvent;
