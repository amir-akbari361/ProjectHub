using ProjectHub.Application.Common;

namespace ProjectHub.Application.Features.Projects;

/// <summary>
/// Centralized catalog of project-related errors as reusable <see cref="Error"/> values. Mirrors
/// <c>AuthErrors</c>: keeping codes here (instead of scattering magic strings across handlers) makes
/// the error CODES a stable contract the UI/API can switch on, and prevents drift between messages.
/// </summary>
public static class ProjectErrors
{
    /// <summary>
    /// Returned when a project id does not resolve to a row (never existed, or soft-deleted and thus
    /// hidden by the global query filter). Modeled as NotFound (404) — the request was well-formed but
    /// the addressed resource is absent.
    /// </summary>
    public static Error NotFound(Guid projectId) => Error.NotFound(
        "Projects.NotFound",
        $"The project with id '{projectId}' was not found.");

    /// <summary>
    /// Returned when an operation is attempted on an archived project (archived projects are read-only).
    /// Modeled as Conflict (409) — the request collided with the resource's current state, not with its
    /// shape. The handler surfaces this instead of letting the domain's DomainException become a 500.
    /// </summary>
    public static readonly Error Archived = Error.Conflict(
        "Projects.Archived",
        "The project is archived and can no longer be modified.");

    /// <summary>
    /// Returned when the caller IS a member of the project but lacks the role required for the
    /// operation (e.g., a Viewer trying to rename, a Maintainer trying to archive). Modeled as
    /// Forbidden (403) — the caller is authenticated and the resource exists and is visible to them,
    /// but their access level is insufficient. This is deliberately distinct from the NotFound we
    /// return to NON-members: hiding a project's existence only makes sense for people who can't see
    /// it at all; a member already knows it exists, so 403 is the honest, correct signal.
    /// </summary>
    public static readonly Error Forbidden = Error.Forbidden(
        "Projects.Forbidden",
        "You do not have permission to perform this action on the project.");
}


