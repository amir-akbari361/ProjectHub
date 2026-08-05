using ProjectHub.Application.Common;

namespace ProjectHub.Application.Features.Comments;

/// <summary>
/// Centralized catalog of comment-related errors as reusable <see cref="Error"/> values. Mirrors
/// <c>TaskErrors</c>, <c>ProjectErrors</c> and <c>AuthErrors</c>: keeping the CODES here (rather than
/// scattering magic strings across handlers) makes the codes a stable contract the UI/API can switch
/// on, and prevents drift between messages.
/// </summary>
public static class CommentErrors
{
    /// <summary>
    /// Returned when a comment id does not resolve to a row the caller can see (never existed,
    /// soft-deleted, or belongs to a task in a project the caller is not a member of). Modeled as
    /// NotFound (404) — like tasks and projects, we collapse "unknown" and "not visible" into one
    /// response to avoid leaking the existence of comments the caller has no access to.
    /// </summary>
    public static Error NotFound(Guid commentId) => Error.NotFound(
        "Comments.NotFound",
        $"The comment with id '{commentId}' was not found.");

    /// <summary>
    /// Returned when the parent task id does not resolve to a task the caller can act on. Modeled as
    /// NotFound (404) for the same information-disclosure reason as above.
    /// </summary>
    public static Error TaskNotFound(Guid taskId) => Error.NotFound(
        "Comments.TaskNotFound",
        $"The task with id '{taskId}' was not found.");

    /// <summary>
    /// Returned when the caller IS a member of the project but lacks the role required to comment
    /// (a Viewer may read the discussion but not post to it), or is not the author of a comment they
    /// are trying to edit. Modeled as Forbidden (403) — the caller is authenticated and the resource
    /// is visible, but their access level is insufficient for this specific action.
    /// </summary>
    public static readonly Error Forbidden = Error.Forbidden(
        "Comments.Forbidden",
        "You do not have permission to perform this action on the comment.");

    /// <summary>
    /// Returned when a comment operation collides with a domain invariant surfaced as a
    /// <c>DomainException</c> (e.g. attempting to edit someone else's comment). Modeled as Conflict
    /// (409) so the domain guard becomes a modeled error instead of a 500.
    /// </summary>
    public static Error Conflict(string message) => Error.Conflict(
        "Comments.Conflict",
        message);
}
