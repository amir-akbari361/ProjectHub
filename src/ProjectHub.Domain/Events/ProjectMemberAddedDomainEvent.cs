using ProjectHub.Domain.Abstractions;
using ProjectHub.Domain.Enums;

namespace ProjectHub.Domain.Events;

public sealed record ProjectMemberAddedDomainEvent(
    Guid ProjectId,
    Guid UserId,
    ProjectRole Role,
    DateTime OccurredAtUtc) : IDomainEvent;
