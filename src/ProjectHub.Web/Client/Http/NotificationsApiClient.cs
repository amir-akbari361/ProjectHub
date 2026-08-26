using ProjectHub.Web.Client.Models;

namespace ProjectHub.Web.Client.Http;

/// <summary>
/// Typed HTTP client for the signed-in user's notification inbox: list, mark one read, mark all read.
/// </summary>
/// <remarks>
/// There is no recipient in any of these URLs. The API resolves the inbox owner from the access token, so a
/// client CANNOT ask for someone else's notifications — the route shape itself closes off that IDOR.
/// </remarks>
public sealed class NotificationsApiClient
{
    private readonly HttpClient _http;

    public NotificationsApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<ApiResult<PagedResult<NotificationItem>>> ListAsync(
        int pageNumber = 1,
        int pageSize = 20,
        bool unreadOnly = false)
    {
        var query = new QueryStringBuilder()
            .Add("pageNumber", pageNumber)
            .Add("pageSize", pageSize)
            .Add("unreadOnly", unreadOnly)
            .Build();

        var response = await _http.GetAsync($"api/notifications{query}");
        return await response.ToResultAsync<PagedResult<NotificationItem>>();
    }

    /// <summary>
    /// Returns how many notifications are unread, WITHOUT transferring them.
    /// </summary>
    /// <remarks>
    /// The API has no dedicated count endpoint, so we ask for the unread list with the smallest legal page
    /// (<c>pageSize=1</c>) and read <c>TotalCount</c> off the envelope. That is one row over the wire instead
    /// of a full page — the paging metadata the server already computes is doing the work. Modelled as its own
    /// method rather than making every caller remember the trick.
    /// </remarks>
    public async Task<ApiResult<int>> GetUnreadCountAsync()
    {
        var result = await ListAsync(pageNumber: 1, pageSize: 1, unreadOnly: true);

        return result.IsSuccess && result.Value is not null
            ? ApiResult<int>.Success(result.Value.TotalCount)
            : ApiResult<int>.Failure(result.Error ?? "Failed to read the unread count.");
    }

    public async Task<ApiResult> MarkAsReadAsync(Guid notificationId)
    {
        var response = await _http.PostAsync($"api/notifications/{notificationId}/read", null);
        return await response.ToResultAsync();
    }

    public async Task<ApiResult> MarkAllAsReadAsync()
    {
        var response = await _http.PostAsync("api/notifications/read-all", null);
        return await response.ToResultAsync();
    }
}
