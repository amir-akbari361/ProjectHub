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

    public async Task<ApiResult<TaskItem>> GetByIdAsync(Guid projectId, Guid taskId)
    {
        var response = await _http.GetAsync($"api/projects/{projectId}/tasks/{taskId}");
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

    public async Task<ApiResult> AssignAsync(Guid projectId, Guid taskId, Guid assigneeId)
    {
        var response = await _http.PostAsJsonAsync(
            $"api/projects/{projectId}/tasks/{taskId}/assign",
            new AssignTaskRequest(assigneeId));
        return await response.ToResultAsync();
    }

    public async Task<ApiResult> ChangeStatusAsync(Guid projectId, Guid taskId, ProjectTaskStatus newStatus)
    {
        var response = await _http.PostAsJsonAsync(
            $"api/projects/{projectId}/tasks/{taskId}/status",
            new ChangeTaskStatusRequest(newStatus));
        return await response.ToResultAsync();
    }

    public async Task<ApiResult> UpdatePriorityAsync(Guid projectId, Guid taskId, TaskPriority priority)
    {
        var response = await _http.PostAsJsonAsync(
            $"api/projects/{projectId}/tasks/{taskId}/priority",
            new UpdateTaskPriorityRequest(priority));
        return await response.ToResultAsync();
    }
}
