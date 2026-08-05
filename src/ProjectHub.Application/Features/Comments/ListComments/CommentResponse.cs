namespace ProjectHub.Application.Features.Comments.ListComments;

/// <summary>
/// The READ-side shape of a single comment in a task's thread. A flat, serialization-friendly projection
/// of the <c>Comment</c> aggregate — never the aggregate itself. It carries only what a thread view needs:
/// who said what, when, and whether it has been edited. The value object (<c>CommentBody</c>) is flattened
/// to a plain string here because the read side has no need for its invariants.
/// </summary>
public sealed record CommentResponse(
    Guid Id,
    Guid TaskId,
    Guid AuthorId,
    string Body,
    bool IsEdited,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
