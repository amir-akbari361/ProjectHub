using ProjectHub.Web.Client.Http;

namespace ProjectHub.Web.Client.State;

/// <summary>
/// Holds the signed-in user's unread-notification count and raises <see cref="Changed"/> whenever it moves, so
/// the app-bar badge and the notifications page never disagree.
/// </summary>
/// <remarks>
/// WHY A SHARED SERVICE RATHER THAN LOCAL STATE IN THE APP BAR?
/// Two components care about the same number and one of them can CHANGE it: the notifications page marks items
/// read, and the badge in <c>MainLayout</c> must drop accordingly. They are in different branches of the
/// component tree, so neither can pass state to the other — a badge owning its own counter would keep showing
/// "5 unread" on a page that visibly has none. Hoisting the number into a circuit-scoped service with a change
/// event is the standard fix, and it keeps both components as passive renderers of one value.
///
/// WHY NO TIMER, AND WHY THE COUNT IS REFRESHED ON NAVIGATION
/// A truly live badge wants a server push, which is what SignalR is for and is deliberately out of scope here.
/// The alternative most reach for — a polling timer — costs a request per interval per open circuit forever,
/// including for idle tabs nobody is looking at, and still lags reality. Refreshing when the user navigates
/// gives an accurate count at every moment they are actually looking at the chrome, for one cheap request per
/// page view, and leaves a clean seam: when a hub is added, it calls <see cref="SetUnreadCount"/> and every
/// consumer updates with no other change.
///
/// WHY THE COUNT REQUEST IS ONE ROW
/// <see cref="NotificationsApiClient.GetUnreadCountAsync"/> asks for a single-item page of unread notifications
/// and reads the envelope's total, so the badge never transfers a payload it will not render.
/// </remarks>
public sealed class NotificationState
{
    private readonly NotificationsApiClient _notificationsApi;
    private readonly ILogger<NotificationState> _logger;

    public NotificationState(
        NotificationsApiClient notificationsApi,
        ILogger<NotificationState> logger)
    {
        _notificationsApi = notificationsApi;
        _logger = logger;
    }

    /// <summary>Raised after the count changes. Subscribers should re-render.</summary>
    public event Action? Changed;

    /// <summary>The number of unread notifications, or 0 before the first successful read.</summary>
    public int UnreadCount { get; private set; }

    /// <summary>Re-reads the count from the API. Safe to call often; a failure leaves the last known value.</summary>
    public async Task RefreshAsync()
    {
        var result = await _notificationsApi.GetUnreadCountAsync();

        if (!result.IsSuccess)
        {
            // A failed badge refresh must never surface as an error to the user — it is ambient chrome, not
            // something they asked for. Log it and keep the previous value rather than flashing the badge to 0,
            // which would read as "all caught up" and is worse than a slightly stale number.
            _logger.LogDebug("Unread notification count refresh failed: {Error}", result.Error);
            return;
        }

        SetUnreadCount(result.Value);
    }

    /// <summary>
    /// Sets the count directly and notifies subscribers if it moved. Used by the notifications page after a
    /// mark-read action so the badge updates without a second round-trip — and the seam a future SignalR hub
    /// would push through.
    /// </summary>
    public void SetUnreadCount(int count)
    {
        // Clamp rather than trusting the input: a negative badge is a rendering bug waiting to happen, and the
        // caller is not necessarily the API.
        var normalized = Math.Max(0, count);

        if (normalized == UnreadCount)
        {
            // No-op guard. Without it, every navigation would raise Changed and re-render the whole app bar
            // even when the number is identical, which is the common case.
            return;
        }

        UnreadCount = normalized;
        Changed?.Invoke();
    }
}
