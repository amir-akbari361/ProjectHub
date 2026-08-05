using System.Net.Http.Json;
using ProjectHub.Web.Client.Models;

namespace ProjectHub.Web.Client.Http;

/// <summary>
/// Typed HTTP client for project CRUD operations. Every method wraps the HTTP response in an
/// <see cref="ApiResult{T}"/> so pages can display success/failure without throwing.
/// </summary>
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
        return await ParseResultAsync<CreateProjectResult>(response);
    }

    public async Task<ApiResult<ProjectDetail>> GetByIdAsync(Guid id)
    {
        var response = await _http.GetAsync($"api/projects/{id}");
        return await ParseResultAsync<ProjectDetail>(response);
    }

    public async Task<ApiResult<PagedResult<ProjectListItem>>> ListAsync(
        int pageNumber = 1,
        int pageSize = 20,
        string? searchTerm = null,
        string? status = null,
        string? sortBy = null,
        bool sortDescending = false)
    {
        var query = $"?pageNumber={pageNumber}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(searchTerm)) query += $"&searchTerm={Uri.EscapeDataString(searchTerm)}";
        if (!string.IsNullOrEmpty(status)) query += $"&status={status}";
        if (!string.IsNullOrEmpty(sortBy)) query += $"&sortBy={sortBy}";
        if (sortDescending) query += "&sortDescending=true";

        var response = await _http.GetAsync($"api/projects{query}");
        return await ParseResultAsync<PagedResult<ProjectListItem>>(response);
    }

    public async Task<ApiResult> UpdateAsync(Guid id, UpdateProjectRequest request)
    {
        var response = await _http.PutAsJsonAsync($"api/projects/{id}", request);
        return await ParseVoidResultAsync(response);
    }

    public async Task<ApiResult> ArchiveAsync(Guid id)
    {
        var response = await _http.PostAsync($"api/projects/{id}/archive", null);
        return await ParseVoidResultAsync(response);
    }

    private static async Task<ApiResult<T>> ParseResultAsync<T>(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            var error = await ExtractErrorAsync(response);
            return ApiResult<T>.Failure(error);
        }

        var result = await response.Content.ReadFromJsonAsync<T>();
        return result is not null
            ? ApiResult<T>.Success(result)
            : ApiResult<T>.Failure("Invalid response from server");
    }

    private static async Task<ApiResult> ParseVoidResultAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            var error = await ExtractErrorAsync(response);
            return ApiResult.Failure(error);
        }

        return ApiResult.Success();
    }

    private static async Task<string> ExtractErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            return problem?.Detail ?? response.ReasonPhrase ?? "Request failed";
        }
        catch
        {
            return response.ReasonPhrase ?? "Request failed";
        }
    }

    private sealed class ProblemDetails
    {
        public string? Detail { get; set; }
    }
}
