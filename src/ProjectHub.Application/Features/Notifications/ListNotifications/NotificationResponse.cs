using ProjectHub.Domain.Enums;

namespace ProjectHub.Application.Features.Notifications.ListNotifications;

/// <summary>
/// The READ-side shape of a single notification in the caller's inbox. A flat,
/// serialization-friendly projection of the <c>Notification</c> aggregate — never the aggregate itself.
/// It carries only what an inbox view needs: what kind of event it was, the rendered message, whether it
/// has been read, and the timestamps used for ordering and "x minutes ago" rendering on the client.
/// </summary>
/// <remarks>
/// WHY IS THERE NO <c>RecipientId</c>?
/// Every row in this list is, by construction, the caller's own — the handler filters by the authenticated
/// user id. Echoing the recipient back would be redundant and would leak an internal key into a payload
/// that never needs it. The read model exposes only what the view consumes.
/// </remarks>
public sealed record NotificationResponse(
    Guid Id,
    NotificationType Type,
    string Message,
    bool IsRead,
    DateTime CreatedAtUtc,
    DateTime? ReadAtUtc);
