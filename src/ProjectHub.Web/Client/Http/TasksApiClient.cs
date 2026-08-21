using System.Net.Http.Json;
using ProjectHub.Domain.Enums;
using ProjectHub.Web.Client.Models;

namespace ProjectHub.Web.Client.Http;

/// <summary>
/// Typed HTTP client for task operations: CRUD, assignment, status transitions, priority changes.
/// The Kanban board and task detail pages consume this. Uses the shared <see cref="HttpResultExtensions"/>
/// helpers so success/error parsing stays consistent with the rest of the app.
/// </summary>
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
        // Item-level route: once a task exists it has its own stable identity, so it is addressed by its
        // own id — the client does NOT need the parent project id to fetch it. This mirrors the API's
        // GET api/tasks/{id}. The previous project-scoped path (api/projects/{id}/tasks/{id}) had no
        // matching server route and always 404'd; carrying an unused projectId was also dead state.
        var response = await _http.GetAsync($"api/tasks/{taskId}");
        return await response.ToResultAsync<TaskItem>();
    }

    public async Task<ApiResult<PagedResult<TaskItem>>> ListAsync(
        Guid projectId,
        int pageNumber = 1,
        int pageSize = 100,
        string? status = null,
        string? priority = null,
        Guid? assigneeId = null)
    {
        var query = $"?pageNumber={pageNumber}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(status)) query += $"&status={status}";
        if (!string.IsNullOrEmpty(priority)) query += $"&priority={priority}";
        if (assigneeId.HasValue) query += $"&assigneeId={assigneeId}";

        var response = await _http.GetAsync($"api/projects/{projectId}/tasks{query}");
        return await response.ToResultAsync<PagedResult<TaskItem>>();
    }

    public async Task<ApiResult> AssignAsync(Guid taskId, Guid assigneeId)
    {
        // Named verb sub-resource on the item route (POST api/tasks/{id}/assign): assignment is a discrete
        // action, not a wholesale replacement of the task, so it is not a PUT and it is keyed only by the
        // task's own id.
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
