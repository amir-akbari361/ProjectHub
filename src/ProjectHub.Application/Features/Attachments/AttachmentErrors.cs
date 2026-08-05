using ProjectHub.Application.Common;

namespace ProjectHub.Application.Features.Attachments;

/// <summary>
/// Centralized catalog of attachment-related errors as reusable <see cref="Error"/> values. Mirrors
/// <c>CommentErrors</c>, <c>TaskErrors</c> and <c>MemberErrors</c>: the codes live here as a stable
/// contract the UI/API can switch on, keeping magic strings out of the handlers and messages consistent.
/// </summary>
public static class AttachmentErrors
{
    /// <summary>
    /// Returned when an attachment id does not resolve to a row the caller can see (never existed,
    /// soft-deleted, or on a task in a project the caller is not a member of). NotFound (404) — we
    /// collapse "unknown" and "not visible" into one response so we never disclose the existence of
    /// attachments the caller has no access to.
    /// </summary>
    public static Error NotFound(Guid attachmentId) => Error.NotFound(
        "Attachments.NotFound",
        $"The attachment with id '{attachmentId}' was not found.");

    /// <summary>
    /// Returned when the parent task id does not resolve to a task the caller can act on. NotFound (404)
    /// for the same information-disclosure reason as above.
    /// </summary>
    public static Error TaskNotFound(Guid taskId) => Error.NotFound(
        "Attachments.TaskNotFound",
        $"The task with id '{taskId}' was not found.");

    /// <summary>
    /// Returned when the caller IS a member of the project but lacks the role required for the action
    /// (a Viewer may download but not upload/delete; a non-uploader non-manager may not delete someone
    /// else's file). Forbidden (403) — authenticated and the resource is visible, but the access level
    /// is insufficient for this specific action.
    /// </summary>
    public static readonly Error Forbidden = Error.Forbidden(
        "Attachments.Forbidden",
        "You do not have permission to perform this action on the attachment.");

    /// <summary>
    /// Returned when an attachment operation collides with a domain invariant surfaced as a
    /// <c>DomainException</c> (e.g. an invalid file name/size that slipped past shape validation).
    /// Conflict (409) so the domain guard becomes a modeled error instead of a 500.
    /// </summary>
    public static Error Conflict(string message) => Error.Conflict(
        "Attachments.Conflict",
        message);
}
