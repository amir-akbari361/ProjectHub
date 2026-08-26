using ProjectHub.Web.Client.Models;

namespace ProjectHub.Web.Client.Http;

/// <summary>
/// Typed HTTP client for global search across the caller's projects and their tasks.
/// </summary>
/// <remarks>
/// WHY THE PARAMETER NAMES MATTER HERE MORE THAN ANYWHERE ELSE
/// The API binds its query string into <c>GlobalSearchRequest(string Q, int PageNumber, int PageSize)</c>.
/// Model binding is by NAME, and an unmatched name is not an error — it is silently dropped and the target
/// property keeps its default. This client previously sent <c>?term=…&amp;limit=…</c>: <c>Q</c> bound to null,
/// so the request failed validation ("search term is required") on every single call, and <c>limit</c> was
/// discarded. Sending <c>q</c> and <c>pageSize</c> is not a style preference; it is the contract.
/// </remarks>
public sealed class SearchApiClient
{
    private readonly HttpClient _http;

    public SearchApiClient(HttpClient http)
    {
        _http = http;
    }

    /// <summary>
    /// Runs a search and returns one PAGE of hits. The API's success shape is a <c>PagedList</c> envelope,
    /// not a bare array — deserializing it into a <c>List&lt;T&gt;</c> (as this client used to) yields an
    /// empty result with no error, because the JSON object simply has no array to bind.
    /// </summary>
    public async Task<ApiResult<PagedResult<SearchResult>>> SearchAsync(
        string term,
        int pageNumber = 1,
        int pageSize = 20)
    {
        var query = $"?q={Uri.EscapeDataString(term)}&pageNumber={pageNumber}&pageSize={pageSize}";

        var response = await _http.GetAsync($"api/search{query}");
        return await response.ToResultAsync<PagedResult<SearchResult>>();
    }
}
