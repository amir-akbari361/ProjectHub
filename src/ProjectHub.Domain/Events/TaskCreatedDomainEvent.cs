using ProjectHub.Domain.Abstractions;

namespace ProjectHub.Domain.Events;

public sealed record TaskCreatedDomainEvent(
    Guid TaskId,
    Guid ProjectId,
    string Title,
    DateTime OccurredAtUtc) : IDomainEvent;
