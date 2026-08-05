using ProjectHub.Application.Abstractions.Messaging;

namespace ProjectHub.Application.Features.Comments.AddComment;

/// <summary>
/// Command to post a new comment onto a task. Carries the parent task id (from the route) and the raw
/// body text (from the request). WRITE side of CQRS. The author is resolved from the authenticated
/// principal inside the handler — it is NEVER accepted from the client, so a comment can never be
/// attributed to someone other than the caller.
/// </summary>
public sealed record AddCommentCommand(
    Guid TaskId,
    string Body)
    : ICommand<AddCommentResponse>;
