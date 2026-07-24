using ProjectHub.Domain.Abstractions;

namespace ProjectHub.Domain.Events;

public sealed record CommentEditedDomainEvent(
    Guid CommentId,
    Guid TaskId,
    DateTime OccurredAtUtc) : IDomainEvent;
