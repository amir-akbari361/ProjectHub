using System.Net.Http.Json;
using ProjectHub.Application.Features.Projects.ListProjects;
using ProjectHub.Domain.Enums;
using ProjectHub.Web.Client.Models;

namespace ProjectHub.Web.Client.Http;

/// <summary>
/// Typed HTTP client for project CRUD. Every method wraps the response in an <see cref="ApiResult{T}"/> so
/// pages can render success or failure without throwing on expected outcomes.
/// </summary>
/// <remarks>
/// WHY THE LIST FILTERS ARE ENUMS AND NOT STRINGS
/// The API binds <c>status</c> to <c>ProjectStatus?</c> and <c>sortBy</c> to <c>ProjectSortBy</c>. Accepting
/// loose strings here (as this client used to) pushed the failure to runtime: a caller passing "created" or
/// "Created_At" compiles fine, binds to nothing, and silently gets the server's default ordering. Typing the
/// parameters against the same enums the server uses makes an invalid sort a COMPILE error, and ASP.NET's
/// query-string binder accepts the enum's name natively.
/// </remarks>
public sealed class ProjectsApiClient
{
    private readonly HttpClient _http;

    public ProjectsApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<ApiResult<CreateProjectResult>> CreateAsync(CreateProjectRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/projects", request);
        return await response.ToResultAsync<CreateProjectResult>();
    }

    public async Task<ApiResult<ProjectDetail>> GetByIdAsync(Guid id)
    {
        var response = await _http.GetAsync($"api/projects/{id}");
        return await response.ToResultAsync<ProjectDetail>();
    }

    /// <summary>
    /// Lists the caller's projects as one page. All three list concerns — paging, filtering, sorting — are
    /// query-string parameters, matching the API's <c>[FromQuery] ListProjectsQuery</c>.
    /// </summary>
    public async Task<ApiResult<PagedResult<ProjectListItem>>> ListAsync(
        int pageNumber = 1,
        int pageSize = 20,
        string? searchTerm = null,
        ProjectStatus? status = null,
        ProjectSortBy sortBy = ProjectSortBy.CreatedAt,
        bool sortDescending = true)
    {
        var query = new QueryStringBuilder()
            .Add("pageNumber", pageNumber)
            .Add("pageSize", pageSize)
            .Add("searchTerm", searchTerm)
            .Add("status", status)
            .Add("sortBy", sortBy)
            .Add("sortDescending", sortDescending)
            .Build();

        var response = await _http.GetAsync($"api/projects{query}");
        return await response.ToResultAsync<PagedResult<ProjectListItem>>();
    }

    public async Task<ApiResult> UpdateAsync(Guid id, UpdateProjectRequest request)
    {
        var response = await _http.PutAsJsonAsync($"api/projects/{id}", request);
        return await response.ToResultAsync();
    }

    public async Task<ApiResult> ArchiveAsync(Guid id)
    {
        var response = await _http.PostAsync($"api/projects/{id}/archive", null);
        return await response.ToResultAsync();
    }
}
