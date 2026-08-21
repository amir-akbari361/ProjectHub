using System.Net.Http.Json;
using ProjectHub.Web.Client.Models;

namespace ProjectHub.Web.Client.Http;

/// <summary>
/// Typed HTTP client for task comments: list, add, edit. Backs the comment thread on the task detail page.
/// </summary>
public sealed class CommentsApiClient
{
    private readonly HttpClient _http;

    public CommentsApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<ApiResult<PagedResult<CommentItem>>> ListAsync(
        Guid taskId,
        int pageNumber = 1,
        int pageSize = 50)
    {
        // The API returns a PagedList envelope (Items + paging metadata), NOT a bare array. Deserializing
        // it into List<CommentItem> silently produced an empty thread because the JSON shape never matched.
        // Mirroring the server contract with PagedResult<T> makes the deserialization total and lets the
        // thread page a long conversation instead of assuming it always fits in one response.
        var response = await _http.GetAsync(
            $"api/tasks/{taskId}/comments?pageNumber={pageNumber}&pageSize={pageSize}");
        return await response.ToResultAsync<PagedResult<CommentItem>>();
    }

    public async Task<ApiResult<AddCommentResult>> AddAsync(Guid taskId, string body)
    {
        var response = await _http.PostAsJsonAsync(
            $"api/tasks/{taskId}/comments",
            new AddCommentRequest(body));
        return await response.ToResultAsync<AddCommentResult>();
    }

    public async Task<ApiResult> EditAsync(Guid commentId, string body)
    {
        // Edit is an ITEM-level operation: once a comment exists it is addressed by its own id
        // (PUT api/comments/{id}), so the caller does not need the parent task id. The old path
        // (api/tasks/{taskId}/comments/{commentId}) had no matching server route and always 404'd.
        var response = await _http.PutAsJsonAsync(
            $"api/comments/{commentId}",
            new EditCommentRequest(body));
        return await response.ToResultAsync();
    }
}
