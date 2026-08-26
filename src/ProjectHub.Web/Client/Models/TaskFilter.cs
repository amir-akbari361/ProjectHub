using ProjectHub.Domain.Enums;

namespace ProjectHub.Web.Client.Models;

/// <summary>
/// The set of filters a task list or board is currently narrowed by. Maps 1:1 onto the API's <c>ListTasks</c>
/// filter parameters.
/// </summary>
/// <remarks>
/// WHY ONE RECORD INSTEAD OF FOUR LOOSE FIELDS
/// A page that held <c>_searchTerm</c>, <c>_status</c>, <c>_priority</c> and <c>_assigneeId</c> separately also
/// needs four change handlers, and each one has to remember to reset the page number — a rule that is enforced
/// only by whoever writes the next handler. Bundling them means "the filters changed" is a single event with a
/// single handler, and the reset happens in exactly one place.
///
/// WHY A RECORD (AND WHY <c>with</c>)
/// Immutability makes a change explicit: the filter bar produces a NEW value via <c>Filter with { Status = x }</c>
/// and raises it, rather than mutating state the parent also holds a reference to. That removes the class of bug
/// where a child's in-place edit is invisible to the parent's change detection and the UI silently fails to
/// re-render.
/// </remarks>
public sealed record TaskFilter
{
    /// <summary>Free-text match against a task's title and description. Null means no text filter.</summary>
    public string? SearchTerm { get; init; }

    /// <summary>Workflow status, or null for all statuses.</summary>
    public ProjectTaskStatus? Status { get; init; }

    /// <summary>Priority, or null for any priority.</summary>
    public TaskPriority? Priority { get; init; }

    /// <summary>Assignee, or null for anyone (including unassigned).</summary>
    public Guid? AssigneeId { get; init; }

    /// <summary>The unfiltered state. A singleton because it is immutable and referenced from every reset.</summary>
    public static TaskFilter None { get; } = new();

    /// <summary>
    /// True when at least one filter is applied. Drives whether "Clear" is enabled, and lets an empty result
    /// distinguish "this project has no tasks" from "no tasks match these filters" — two situations with
    /// completely different remedies.
    /// </summary>
    public bool IsActive =>
        !string.IsNullOrWhiteSpace(SearchTerm)
        || Status is not null
        || Priority is not null
        || AssigneeId is not null;
}
