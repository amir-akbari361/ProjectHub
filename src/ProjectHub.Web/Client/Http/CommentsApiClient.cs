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

    public async Task<ApiResult<List<CommentItem>>> ListAsync(Guid taskId)
    {
        var response = await _http.GetAsync($"api/tasks/{taskId}/comments");
        return await response.ToResultAsync<List<CommentItem>>();
    }

    public async Task<ApiResult<AddCommentResult>> AddAsync(Guid taskId, string body)
    {
        var response = await _http.PostAsJsonAsync(
            $"api/tasks/{taskId}/comments",
            new AddCommentRequest(body));
        return await response.ToResultAsync<AddCommentResult>();
    }

    public async Task<ApiResult> EditAsync(Guid taskId, Guid commentId, string body)
    {
        var response = await _http.PutAsJsonAsync(
            $"api/tasks/{taskId}/comments/{commentId}",
            new EditCommentRequest(body));
        return await response.ToResultAsync();
    }
}
