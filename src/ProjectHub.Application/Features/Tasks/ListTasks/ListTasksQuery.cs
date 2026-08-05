using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Common;
using ProjectHub.Application.Features.Tasks.GetTaskById;
using ProjectHub.Domain.Enums;

namespace ProjectHub.Application.Features.Tasks.ListTasks;

/// <summary>
/// Query to list the tasks of a single project as one page. READ side of CQRS, carrying the three
/// orthogonal list concerns — pagination, filtering, sorting — as explicit inputs. Scoped to a
/// <see cref="ProjectId"/> (from the route) because a task board is always viewed per project.
/// </summary>
/// <remarks>
/// WHY DEFAULTS ON THE RECORD? A caller may omit page/size; defaulting here keeps the query well-formed
/// before the pipeline runs, and the validator then CLAMPS the range so a client cannot request an
/// unbounded page (a DoS vector). WHY IS THE CALLER'S ID ABSENT? Visibility is derived from the
/// authenticated principal inside the handler — never from client input.
/// </remarks>
public sealed record ListTasksQuery(
    Guid ProjectId,
    int PageNumber = 1,
    int PageSize = 20,
    string? SearchTerm = null,
    ProjectTaskStatus? Status = null,
    TaskPriority? Priority = null,
    Guid? AssigneeId = null,
    TaskSortBy SortBy = TaskSortBy.CreatedAt,
    bool SortDescending = true)
    : IQuery<PagedList<TaskResponse>>;

/// <summary>
/// The whitelisted set of columns a client may sort tasks by. An ENUM, not a free-text string, so the
/// sort surface is a closed, reviewable contract and a client can never steer ORDER BY toward an
/// unindexed or sensitive column.
/// </summary>
public enum TaskSortBy
{
    Title = 0,
    CreatedAt = 1,
    Status = 2,
    Priority = 3
}
