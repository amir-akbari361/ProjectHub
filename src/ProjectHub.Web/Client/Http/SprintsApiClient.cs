using System.Net.Http.Json;
using ProjectHub.Web.Client.Models;

namespace ProjectHub.Web.Client.Http;

/// <summary>
/// Typed HTTP client for sprint operations: create, list, and the two lifecycle transitions (start,
/// complete). Consumed by the Sprints tab on the project detail page.
/// </summary>
/// <remarks>
/// WHY TWO ROUTE SHAPES (MIRRORING THE API)
/// Collection operations are sub-resources of a project — <c>api/projects/{projectId}/sprints</c> — because
/// a sprint is created INSIDE a project. Once it exists it has its own identity, so the lifecycle actions
/// key off <c>api/sprints/{id}</c> and need no project id. The client mirrors <see cref="TasksApiClient"/>
/// so the URL space has a single source of truth.
/// </remarks>
public sealed class SprintsApiClient
{
    private readonly HttpClient _http;

    public SprintsApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<ApiResult<CreateSprintResult>> CreateAsync(Guid projectId, CreateSprintRequest request)
    {
        var response = await _http.PostAsJsonAsync($"api/projects/{projectId}/sprints", request);
        return await response.ToResultAsync<CreateSprintResult>();
    }

    /// <summary>
    /// Lists a project's sprints, ordered by start date. Not paged — a project has few sprints — so this
    /// returns a plain list rather than the <see cref="PagedResult{T}"/> envelope the task list uses.
    /// </summary>
    public async Task<ApiResult<List<SprintItem>>> ListAsync(Guid projectId)
    {
        var response = await _http.GetAsync($"api/projects/{projectId}/sprints");
        return await response.ToResultAsync<List<SprintItem>>();
    }

    public async Task<ApiResult> StartAsync(Guid sprintId)
    {
        // Named verb sub-resource on the item route: starting is a discrete lifecycle action, not a
        // wholesale replacement of the sprint, so it is a POST to .../start. There is no body — the id in
        // the route is the entire request — so we post null content.
        var response = await _http.PostAsync($"api/sprints/{sprintId}/start", content: null);
        return await response.ToResultAsync();
    }

    public async Task<ApiResult> CompleteAsync(Guid sprintId)
    {
        var response = await _http.PostAsync($"api/sprints/{sprintId}/complete", content: null);
        return await response.ToResultAsync();
    }
}
