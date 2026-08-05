using ProjectHub.Application.Common;

namespace ProjectHub.Application.Features.ProjectMembers;

/// <summary>
/// Centralized catalog of membership-related errors as reusable <see cref="Error"/> values. Mirrors
/// <c>ProjectErrors</c>/<c>CommentErrors</c>: codes live here (not as magic strings inside handlers) so
/// the error CODES stay a stable contract the UI/API can switch on, and message drift is impossible.
/// </summary>
public static class MemberErrors
{
    /// <summary>
    /// Returned when the addressed project does not resolve to a visible row for the caller (unknown id,
    /// soft-deleted, or the caller is not a member). Collapsed into a single NotFound (404) so we never
    /// disclose the existence of a project to someone who has no business seeing it.
    /// </summary>
    public static Error ProjectNotFound(Guid projectId) => Error.NotFound(
        "Members.ProjectNotFound",
        $"The project with id '{projectId}' was not found.");

    /// <summary>
    /// Returned when the user being added/changed does not exist as an account. Modeled as NotFound (404)
    /// — the request references a principal that cannot be resolved.
    /// </summary>
    public static Error UserNotFound(Guid userId) => Error.NotFound(
        "Members.UserNotFound",
        $"The user with id '{userId}' was not found.");

    /// <summary>
    /// Returned when the caller is a member but lacks the role required to manage membership (managing
    /// members is an Owner/Maintainer action). Forbidden (403) — the caller can see the project but their
    /// access level is insufficient; deliberately distinct from the NotFound shown to non-members.
    /// </summary>
    public static readonly Error Forbidden = Error.Forbidden(
        "Members.Forbidden",
        "You do not have permission to manage members of this project.");

    /// <summary>
    /// Returned when only an Owner may perform the action (e.g. granting or revoking the Owner role) and
    /// the caller is a mere Maintainer. Forbidden (403) — a finer-grained cousin of <see cref="Forbidden"/>.
    /// </summary>
    public static readonly Error OwnerOnly = Error.Forbidden(
        "Members.OwnerOnly",
        "Only a project owner may grant or revoke the owner role.");

    /// <summary>
    /// Wraps a domain invariant rejection (already a member, last owner, archived project, etc.) as a
    /// Conflict (409). The domain's DomainException carries the precise reason; we surface its message
    /// rather than letting it escape as a 500.
    /// </summary>
    public static Error Conflict(string message) => Error.Conflict(
        "Members.Conflict",
        message);
}
