using ProjectHub.Application.Abstractions.Messaging;

namespace ProjectHub.Application.Features.Comments.EditComment;

/// <summary>
/// Command to change the body of an existing comment. Carries the comment id (from the route) and the
/// new body text (from the request). WRITE side of CQRS. The editor is resolved from the authenticated
/// principal inside the handler and passed to the domain, which enforces the "only the author may edit"
/// invariant — the client can never claim to be someone else.
/// </summary>
public sealed record EditCommentCommand(
    Guid CommentId,
    string Body)
    : ICommand;
