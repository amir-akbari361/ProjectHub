namespace ProjectHub.Application.Abstractions.Services;

/// <summary>
/// Delivers a freshly created notification to its recipient over a live connection, so an inbox updates
/// without the user navigating or refreshing. This is a PORT: the Application layer decides WHAT to send
/// and to WHOM, while the host supplies the transport (SignalR today) — exactly the arrangement used for
/// <see cref="ICurrentUser"/> and <see cref="IDateTimeProvider"/>.
/// </summary>
/// <remarks>
/// WHY A PORT INSTEAD OF INJECTING IHubContext DIRECTLY?
/// The push is triggered by a MediatR notification handler, and MediatR only scans the Application
/// assembly — but <c>IHubContext&lt;NotificationHub&gt;</c> is a hosting concern that lives with the hub in
/// the API project. Inverting the dependency keeps the handler in the assembly MediatR discovers while the
/// SignalR-specific adapter stays at the composition root.
///
/// DELIVERY IS BEST-EFFORT BY DESIGN.
/// The notification row is already committed before a push is attempted, so the durable inbox is the source
/// of truth and the wire message is only an optimization. An offline recipient, a dropped socket, or a
/// failed send must never surface as an error — the client reconciles by re-reading its unread count on
/// (re)connect. Implementations therefore swallow transport faults rather than throwing.
/// </remarks>
public interface INotificationPusher
{
    Task PushAsync(Guid recipientId, NotificationPush push, CancellationToken cancellationToken);
}

/// <summary>
/// The payload delivered to a connected recipient. Carries enough for both halves of the live UX: the
/// <see cref="UnreadCount"/> drives the badge, while <see cref="Message"/> and <see cref="Type"/> drive the
/// toast — so the client needs no follow-up HTTP round-trip to render either.
/// </summary>
/// <remarks>
/// <see cref="Type"/> is the enum NAME rather than its numeric value: this record IS the wire contract, and
/// a name survives a future reordering of <c>NotificationType</c> that would silently re-map an integer.
///
/// <see cref="UnreadCount"/> is an absolute count, never a delta. A client that misses a push (offline,
/// mid-reconnect) would drift permanently if it were incrementing locally; an absolute value makes every
/// message self-healing — the newest one always wins.
/// </remarks>
public sealed record NotificationPush(
    Guid NotificationId,
    string Type,
    string Message,
    DateTime CreatedAtUtc,
    int UnreadCount);
