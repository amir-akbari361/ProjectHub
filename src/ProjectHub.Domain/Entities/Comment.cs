using ProjectHub.Domain.Common;
using ProjectHub.Domain.Events;
using ProjectHub.Domain.Exceptions;
using ProjectHub.Domain.Primitives;
using ProjectHub.Domain.ValueObjects;

namespace ProjectHub.Domain.Entities;

public sealed class Comment : AggregateRoot
{
    private Comment(Guid id, Guid taskId, Guid authorId, CommentBody body)
        : base(id)
    {
        TaskId = taskId;
        AuthorId = authorId;
        Body = body;
    }

    private Comment()
        : base(Guid.Empty)
    {
        Body = null!;
    }

    public Guid TaskId { get; private set; }

    public Guid AuthorId { get; private set; }

    public CommentBody Body { get; private set; }

    public bool IsEdited { get; private set; }

    public static Comment Create(Guid taskId, Guid authorId, CommentBody body, DateTime utcNow)
    {
        Guard.NotEmpty(taskId, nameof(taskId));
        Guard.NotEmpty(authorId, nameof(authorId));
        Guard.NotNull(body, nameof(body));

        var comment = new Comment(Guid.NewGuid(), taskId, authorId, body);
        comment.MarkCreated(utcNow, authorId);
        comment.RaiseDomainEvent(new CommentAddedDomainEvent(comment.Id, taskId, authorId, utcNow));

        return comment;
    }

    public void Edit(CommentBody newBody, Guid editorId, DateTime utcNow)
    {
        Guard.NotNull(newBody, nameof(newBody));

        if (editorId != AuthorId)
        {
            throw new DomainException("Only the author can edit their comment.");
        }

        Body = newBody;
        IsEdited = true;
        MarkUpdated(utcNow, editorId);
        RaiseDomainEvent(new CommentEditedDomainEvent(Id, TaskId, utcNow));
    }
}
