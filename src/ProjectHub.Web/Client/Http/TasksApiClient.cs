using System.Net.Http.Json;
using ProjectHub.Application.Features.Tasks.ListTasks;
using ProjectHub.Domain.Enums;
using ProjectHub.Web.Client.Models;

namespace ProjectHub.Web.Client.Http;

/// <summary>
/// Typed HTTP client for task operations: create, read, list, assignment, status transitions, and priority
/// changes. Consumed by the project detail list, the Kanban board, and the task drawer.
/// </summary>
/// <remarks>
/// WHY TWO ROUTE SHAPES (MIRRORING THE API)
/// Collection operations are sub-resources of a project — <c>api/projects/{projectId}/tasks</c> — because a
/// task is created INSIDE a project. Once it exists it has its own identity, so item operations key off
/// <c>api/tasks/{id}</c> and need no project id. The client mirrors the controller exactly so the URL space
/// has a single source of truth.
/// </remarks>
public sealed class TasksApiClient
{
    private readonly HttpClient _http;

    public TasksApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<ApiResult<CreateTaskResult>> CreateAsync(Guid projectId, CreateTaskRequest request)
    {
        var response = await _http.PostAsJsonAsync($"api/projects/{projectId}/tasks", request);
        return await response.ToResultAsync<CreateTaskResult>();
    }

    public async Task<ApiResult<TaskItem>> GetByIdAsync(Guid taskId)
    {
        // Item-level route: once a task exists it is addressed by its own id, so the caller does NOT need the
        // parent project id. This mirrors GET api/tasks/{id}. The previous project-scoped path had no matching
        // server route and always 404'd, and the unused projectId parameter was dead state.
        var response = await _http.GetAsync($"api/tasks/{taskId}");
        return await response.ToResultAsync<TaskItem>();
    }

    /// <summary>
    /// Lists a project's tasks as one page, with the server-side filter and sort surface exposed as typed
    /// parameters. Filters are nullable — null means "do not filter", which is how the API distinguishes an
    /// absent filter from a deliberate one.
    /// </summary>
    public async Task<ApiResult<PagedResult<TaskItem>>> ListAsync(
        Guid projectId,
        int pageNumber = 1,
        int pageSize = 100,
        string? searchTerm = null,
        ProjectTaskStatus? status = null,
        TaskPriority? priority = null,
        Guid? assigneeId = null,
        TaskSortBy sortBy = TaskSortBy.CreatedAt,
        bool sortDescending = true)
    {
        var query = new QueryStringBuilder()
            .Add("pageNumber", pageNumber)
            .Add("pageSize", pageSize)
            .Add("searchTerm", searchTerm)
            .Add("status", status)
            .Add("priority", priority)
            .Add("assigneeId", assigneeId)
            .Add("sortBy", sortBy)
            .Add("sortDescending", sortDescending)
            .Build();

        var response = await _http.GetAsync($"api/projects/{projectId}/tasks{query}");
        return await response.ToResultAsync<PagedResult<TaskItem>>();
    }

    public async Task<ApiResult> AssignAsync(Guid taskId, Guid assigneeId)
    {
        // Named verb sub-resource on the item route: assignment is a discrete action, not a wholesale
        // replacement of the task, so it is a POST to .../assign rather than a PUT on the task itself.
        var response = await _http.PostAsJsonAsync(
            $"api/tasks/{taskId}/assign",
            new AssignTaskRequest(assigneeId));
        return await response.ToResultAsync();
    }

    public async Task<ApiResult> ChangeStatusAsync(Guid taskId, ProjectTaskStatus newStatus)
    {
        var response = await _http.PostAsJsonAsync(
            $"api/tasks/{taskId}/status",
            new ChangeTaskStatusRequest(newStatus));
        return await response.ToResultAsync();
    }

    public async Task<ApiResult> UpdatePriorityAsync(Guid taskId, TaskPriority priority)
    {
        var response = await _http.PostAsJsonAsync(
            $"api/tasks/{taskId}/priority",
            new UpdateTaskPriorityRequest(priority));
        return await response.ToResultAsync();
    }
}
