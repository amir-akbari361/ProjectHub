using ProjectHub.Domain.Abstractions;

namespace ProjectHub.Domain.Events;

public sealed record CommentAddedDomainEvent(
    Guid CommentId,
    Guid TaskId,
    Guid AuthorId,
    DateTime OccurredAtUtc) : IDomainEvent;
