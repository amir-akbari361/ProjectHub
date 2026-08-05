using ProjectHub.Application.Abstractions.Messaging;

namespace ProjectHub.Application.Features.Notifications.MarkAsRead;

/// <summary>
/// Command to mark a SINGLE notification — identified by <see cref="NotificationId"/> from the route — as
/// read on behalf of the authenticated caller. WRITE side of CQRS. Returns no payload: the client already
/// knows the id it acted on, and a re-fetch of the inbox reflects the new state.
/// </summary>
/// <remarks>
/// WHY NO RECIPIENT ON THE COMMAND?
/// Just like the query, the recipient is the authenticated principal, never a client input. The handler
/// loads the notification and confirms it belongs to the caller before mutating — a caller can never mark
/// someone else's notification as read (no IDOR).
/// </remarks>
public sealed record MarkNotificationAsReadCommand(Guid NotificationId) : ICommand;
