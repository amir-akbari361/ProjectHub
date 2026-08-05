using ProjectHub.Web.Client.Models;

namespace ProjectHub.Web.Client.Http;

/// <summary>
/// Typed HTTP client for the current user's notifications: list, mark one read, mark all read.
/// Backs the notification bell in the app bar.
/// </summary>
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
        var query = $"?pageNumber={pageNumber}&pageSize={pageSize}";
        if (unreadOnly) query += "&unreadOnly=true";

        var response = await _http.GetAsync($"api/notifications{query}");
        return await response.ToResultAsync<PagedResult<NotificationItem>>();
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
