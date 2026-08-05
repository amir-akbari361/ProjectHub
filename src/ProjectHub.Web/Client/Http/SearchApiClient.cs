using ProjectHub.Web.Client.Models;

namespace ProjectHub.Web.Client.Http;

/// <summary>
/// Typed HTTP client for global search across projects and tasks. Backs the search bar in the app bar.
/// </summary>
public sealed class SearchApiClient
{
    private readonly HttpClient _http;

    public SearchApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<ApiResult<List<SearchResult>>> SearchAsync(string term, int limit = 20)
    {
        var query = $"?term={Uri.EscapeDataString(term)}&limit={limit}";
        var response = await _http.GetAsync($"api/search{query}");
        return await response.ToResultAsync<List<SearchResult>>();
    }
}
