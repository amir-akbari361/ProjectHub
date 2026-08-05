using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectHub.Application.Common;
using ProjectHub.Application.Features.Tasks.AssignTask;
using ProjectHub.Application.Features.Tasks.ChangeTaskStatus;
using ProjectHub.Application.Features.Tasks.CreateTask;
using ProjectHub.Application.Features.Tasks.GetTaskById;
using ProjectHub.Application.Features.Tasks.ListTasks;
using ProjectHub.Application.Features.Tasks.UpdateTaskPriority;
using ProjectHub.Domain.Enums;

namespace ProjectHub.API.Controllers;

/// <summary>
/// The HTTP entry point for the task use cases. Like <see cref="ProjectsController"/> every action is a
/// THIN adapter: it stitches the route/body into a command or query, dispatches it through MediatR, and
/// hands the resulting <c>Result</c> to <see cref="ApiController.HandleResult"/> for HTTP mapping. No
/// business logic lives here — authorization, membership checks, and invariants are all enforced in the
/// Application handlers and the domain aggregate.
/// </summary>
/// <remarks>
/// WHY TWO ROUTE SHAPES IN ONE CONTROLLER?
/// A task is a CHILD of a project, so the collection-level operations (create, list) read most naturally
/// as sub-resources of a project: <c>/api/projects/{projectId}/tasks</c>. But once a task exists it has
/// its own stable identity, so the item-level operations (get, assign, status, priority) hang off
/// <c>/api/tasks/{id}</c> — a client that holds a task id shouldn't need to know its project to act on
/// it. We express the project-scoped routes with absolute templates ("~/...") so they escape the
/// controller's default <c>api/[controller]</c> prefix, and leave the item routes to inherit it.
///
/// Every action is <c>[Authorize]</c> (secure by default): tasks are always acted on BY a known member
/// and attributed to them, so a request without a valid token fails closed with 401.
/// </remarks>
[Authorize]
public sealed class TasksController : ApiController
{
    private readonly ISender _sender;

    public TasksController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Creates a new task inside a project. The project id comes from the ROUTE and the task fields from
    /// the BODY; we stitch them into the command so the parent identity can never be spoofed by a
    /// mismatched body field. Returns <c>201 Created</c> with the new task's id and normalized title, and
    /// a <c>Location</c> header pointing at the item-level GET endpoint.
    /// </summary>
    [HttpPost("~/api/projects/{projectId:guid}/tasks")]
    [ProducesResponseType(typeof(CreateTaskResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        Guid projectId,
        CreateTaskRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateTaskCommand(
            projectId, request.Title, request.Description, request.Priority);

        var result = await _sender.Send(command, cancellationToken);

        return HandleResult(result, value => CreatedAtAction(
            actionName: nameof(GetById),
            routeValues: new { id = value.Id },
            value: value));
    }

    /// <summary>
    /// Lists a project's tasks as a single page. Pagination, filtering, and sorting arrive as
    /// QUERY-STRING parameters because they refine a GET on a collection; the project id is bound from the
    /// route and combined with the bound query into the full <see cref="ListTasksQuery"/>. Returns
    /// <c>200 OK</c> with a <see cref="PagedList{T}"/> envelope.
    /// </summary>
    [HttpGet("~/api/projects/{projectId:guid}/tasks")]
    [ProducesResponseType(typeof(PagedList<TaskResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List(
        Guid projectId,
        [FromQuery] ListTasksRequest request,
        CancellationToken cancellationToken)
    {
        var query = new ListTasksQuery(
            projectId,
            request.PageNumber,
            request.PageSize,
            request.SearchTerm,
            request.Status,
            request.Priority,
            request.AssigneeId,
            request.SortBy,
            request.SortDescending);

        var result = await _sender.Send(query, cancellationToken);

        return HandleResult(result);
    }

    /// <summary>
    /// Fetches a single task the caller can see. Returns <c>200 OK</c> with the projection, or
    /// <c>404 Not Found</c> if the id is unknown OR the caller is not a member of its project (the two are
    /// deliberately indistinguishable to avoid information disclosure).
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TaskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetTaskByIdQuery(id), cancellationToken);

        return HandleResult(result);
    }

    /// <summary>
    /// (Re)assigns a task to a project member. The task id comes from the route and the assignee from the
    /// body. Returns <c>204 No Content</c> on success, <c>403</c> if the caller lacks a mutating role,
    /// <c>404</c> if the task is unknown/invisible, or <c>409</c> if the assignee is not a project member.
    /// </summary>
    /// <remarks>
    /// WHY <c>POST .../assign</c> AND NOT <c>PUT</c>? Assignment is a discrete named action against an
    /// existing resource, not a wholesale replacement of the task representation, so a verb sub-resource
    /// communicates intent more precisely than PUT on the task itself.
    /// </remarks>
    [HttpPost("{id:guid}/assign")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Assign(
        Guid id,
        AssignTaskRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new AssignTaskCommand(id, request.AssigneeId), cancellationToken);

        return HandleResult(result);
    }

    /// <summary>
    /// Transitions a task to a new workflow status. Returns <c>204 No Content</c> on success,
    /// <c>403</c> if the caller lacks a mutating role, <c>404</c> if the task is unknown/invisible, or
    /// <c>409</c> if the task is already in the requested status.
    /// </summary>
    [HttpPost("{id:guid}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeStatus(
        Guid id,
        ChangeTaskStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new ChangeTaskStatusCommand(id, request.NewStatus), cancellationToken);

        return HandleResult(result);
    }

    /// <summary>
    /// Changes a task's priority. Returns <c>204 No Content</c> on success, <c>403</c> if the caller
    /// lacks a mutating role, or <c>404</c> if the task is unknown/invisible. Setting the same priority is
    /// idempotent, so there is no 409 here.
    /// </summary>
    [HttpPost("{id:guid}/priority")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePriority(
        Guid id,
        UpdateTaskPriorityRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new UpdateTaskPriorityCommand(id, request.Priority), cancellationToken);

        return HandleResult(result);
    }
}

/// <summary>
/// Request body for <see cref="TasksController.Create"/>. Carries ONLY client-supplied fields; the parent
/// project id is owned by the route and stitched in by the controller, so it can never be double-sourced.
/// </summary>
public sealed record CreateTaskRequest(string Title, string? Description, TaskPriority Priority);

/// <summary>
/// Query-string binding target for <see cref="TasksController.List"/>. Mirrors the list concerns of
/// <c>ListTasksQuery</c> WITHOUT the project id (which is a route value). Kept separate from the query so
/// the wire contract and the internal query can evolve independently and the id is never double-sourced.
/// Defaults match the query's own defaults so a bare <c>GET</c> is valid.
/// </summary>
public sealed record ListTasksRequest(
    int PageNumber = 1,
    int PageSize = 20,
    string? SearchTerm = null,
    ProjectTaskStatus? Status = null,
    TaskPriority? Priority = null,
    Guid? AssigneeId = null,
    TaskSortBy SortBy = TaskSortBy.CreatedAt,
    bool SortDescending = true);

/// <summary>Request body for <see cref="TasksController.Assign"/>. The task id is owned by the route.</summary>
public sealed record AssignTaskRequest(Guid AssigneeId);

/// <summary>Request body for <see cref="TasksController.ChangeStatus"/>. The task id is owned by the route.</summary>
public sealed record ChangeTaskStatusRequest(ProjectTaskStatus NewStatus);

/// <summary>Request body for <see cref="TasksController.UpdatePriority"/>. The task id is owned by the route.</summary>
public sealed record UpdateTaskPriorityRequest(TaskPriority Priority);
