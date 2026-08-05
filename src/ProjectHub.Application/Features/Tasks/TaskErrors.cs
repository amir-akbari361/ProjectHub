using ProjectHub.Application.Common;

namespace ProjectHub.Application.Features.Tasks;

/// <summary>
/// Centralized catalog of task-related errors as reusable <see cref="Error"/> values. Mirrors
/// <c>ProjectErrors</c> and <c>AuthErrors</c>: keeping codes here (instead of scattering magic strings
/// across handlers) makes the error CODES a stable contract the UI/API can switch on, and prevents
/// drift between messages.
/// </summary>
public static class TaskErrors
{
    /// <summary>
    /// Returned when a task id does not resolve to a row the caller can see (never existed,
    /// soft-deleted, or belongs to a project the caller is not a member of). Modeled as NotFound (404)
    /// — like projects, we collapse "unknown" and "not visible" into one response to avoid leaking the
    /// existence of tasks in projects the caller has no access to.
    /// </summary>
    public static Error NotFound(Guid taskId) => Error.NotFound(
        "Tasks.NotFound",
        $"The task with id '{taskId}' was not found.");

    /// <summary>
    /// Returned when the parent project id does not resolve to a project the caller can act on.
    /// Modeled as NotFound (404) for the same information-disclosure reason as above.
    /// </summary>
    public static Error ProjectNotFound(Guid projectId) => Error.NotFound(
        "Tasks.ProjectNotFound",
        $"The project with id '{projectId}' was not found.");

    /// <summary>
    /// Returned when the caller IS a member of the project but lacks the role required to mutate tasks
    /// (a Viewer may read the board but not create/assign/transition tasks). Modeled as Forbidden (403)
    /// — the caller is authenticated and the resource is visible, but their access level is insufficient.
    /// </summary>
    public static readonly Error Forbidden = Error.Forbidden(
        "Tasks.Forbidden",
        "You do not have permission to perform this action on the task.");

    /// <summary>
    /// Returned when a task operation collides with the task's current state (e.g., transitioning a task
    /// to the status it is already in). Modeled as Conflict (409) — the request collided with the
    /// resource's current state, not with its shape. Surfaces the domain's DomainException as a modeled
    /// error instead of a 500.
    /// </summary>
    public static Error Conflict(string message) => Error.Conflict(
        "Tasks.Conflict",
        message);
}
