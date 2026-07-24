using ProjectHub.Domain.Abstractions;

namespace ProjectHub.Domain.Events;

public sealed record ProjectMemberRemovedDomainEvent(
    Guid ProjectId,
    Guid UserId,
    DateTime OccurredAtUtc) : IDomainEvent;
