using Microsoft.AspNetCore.SignalR;
using ProjectHub.Application.Abstractions.Services;

namespace ProjectHub.API.Hubs;

/// <summary>
/// The SignalR adapter for <see cref="INotificationPusher"/>: the concrete transport behind the
/// Application layer's port. It lives in the API project because that is where the hub is hosted, and it
/// does nothing but address and send — recipient resolution, message text, and the unread count are all
/// decided by <c>NotificationPushHandler</c>, keeping this class free of any business rule.
/// </summary>
/// <remarks>
/// <c>Clients.User(...)</c> fans out to EVERY connection that user currently has (several tabs, phone and
/// laptop), which is what makes the badge consistent across their open sessions. Users with no live
/// connection are a no-op — their notification is already durably stored and will be read on next load.
///
/// SENDING NEVER THROWS OUTWARD. <c>SendAsync</c> can fail for reasons entirely outside this request (a
/// socket closing mid-send, a backplane hiccup), and the caller is running post-commit where an exception
/// would corrupt nothing but would pollute logs with false alarms about an operation that actually
/// succeeded. The port documents delivery as best-effort, so a fault is logged at debug level and
/// swallowed here, closest to the transport that produced it.
/// </remarks>
internal sealed class SignalRNotificationPusher : INotificationPusher
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<SignalRNotificationPusher> _logger;

    public SignalRNotificationPusher(
        IHubContext<NotificationHub> hubContext,
        ILogger<SignalRNotificationPusher> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task PushAsync(
        Guid recipientId,
        NotificationPush push,
        CancellationToken cancellationToken)
    {
        try
        {
            await _hubContext.Clients
                .User(recipientId.ToString())
                .SendAsync(NotificationHub.NotificationReceived, push, cancellationToken);

            _logger.LogDebug(
                "Pushed notification {NotificationId} to recipient {RecipientId} (unread={UnreadCount}).",
                push.NotificationId, recipientId, push.UnreadCount);
        }
        catch (Exception exception)
        {
            // The row is committed; a failed push only costs this user a live update, not the notification.
            _logger.LogDebug(
                exception,
                "Could not deliver notification {NotificationId} to recipient {RecipientId} over SignalR.",
                push.NotificationId, recipientId);
        }
    }
}
