using ProjectHub.Domain.Abstractions;

namespace ProjectHub.Domain.Events;

public sealed record ProjectArchivedDomainEvent(Guid ProjectId, DateTime OccurredAtUtc)
    : IDomainEvent;