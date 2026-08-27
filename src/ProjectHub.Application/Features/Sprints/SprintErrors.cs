using ProjectHub.Application.Common;

namespace ProjectHub.Application.Features.Sprints;

/// <summary>
/// Centralized catalog of sprint-related errors as reusable <see cref="Error"/> values. Mirrors
/// <c>TaskErrors</c> and <c>ProjectErrors</c>: keeping codes here (instead of scattering magic strings
/// across handlers) makes the error CODES a stable contract the UI/API can switch on, and prevents drift
/// between messages.
/// </summary>
public static class SprintErrors
{
    /// <summary>
    /// Returned when a sprint id does not resolve to a row the caller can see (never existed,
    /// soft-deleted, or belongs to a project the caller is not a member of). Modeled as NotFound (404) —
    /// like tasks, we collapse "unknown" and "not visible" into one response to avoid leaking the
    /// existence of sprints in projects the caller has no access to.
    /// </summary>
    public static Error NotFound(Guid sprintId) => Error.NotFound(
        "Sprints.NotFound",
        $"The sprint with id '{sprintId}' was not found.");

    /// <summary>
    /// Returned when the parent project id does not resolve to a project the caller can act on.
    /// Modeled as NotFound (404) for the same information-disclosure reason as above.
    /// </summary>
    public static Error ProjectNotFound(Guid projectId) => Error.NotFound(
        "Sprints.ProjectNotFound",
        $"The project with id '{projectId}' was not found.");

    /// <summary>
    /// Returned when the caller IS a member of the project but lacks the role required to manage sprints
    /// (a Viewer may see them but not create/start/complete). Modeled as Forbidden (403) — the caller is
    /// authenticated and the resource is visible, but their access level is insufficient.
    /// </summary>
    public static readonly Error Forbidden = Error.Forbidden(
        "Sprints.Forbidden",
        "You do not have permission to manage sprints in this project.");

    /// <summary>
    /// Returned when a sprint operation collides with the sprint's current state (e.g., starting a sprint
    /// that is not Planned, or completing one that is not Active). Modeled as Conflict (409) — the request
    /// collided with the resource's current state, not with its shape. Surfaces the domain's
    /// DomainException as a modeled error instead of a 500.
    /// </summary>
    public static Error Conflict(string message) => Error.Conflict(
        "Sprints.Conflict",
        message);

    /// <summary>
    /// Returned when the requested schedule is not a valid <c>DateRange</c> (end not after start, or a
    /// non-UTC boundary that survived shape validation). Modeled as Validation (400) — the request's shape
    /// is wrong. Surfaces the domain's DomainException from <c>DateRange.Create</c> as a modeled error.
    /// </summary>
    public static Error InvalidSchedule(string message) => Error.Validation(
        "Sprints.InvalidSchedule",
        message);
}
