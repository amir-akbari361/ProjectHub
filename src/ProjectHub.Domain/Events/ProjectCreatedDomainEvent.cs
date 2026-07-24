using ProjectHub.Domain.Abstractions;

namespace ProjectHub.Domain.Events;

public sealed record ProjectCreatedDomainEvent(Guid ProjectId, string Name, DateTime OccurredAtUtc)
    : IDomainEvent;