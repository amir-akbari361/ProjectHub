using ProjectHub.Application.Abstractions.Messaging;

namespace ProjectHub.Application.Features.Notifications.MarkAllAsRead;

/// <summary>
/// Command to mark EVERY unread notification of the authenticated caller as read in one shot — the
/// "mark all as read" / "clear the badge" action. WRITE side of CQRS. It carries NO fields at all: the
/// only input is the identity of the caller, which comes from the token, never the body.
/// </summary>
/// <remarks>
/// WHY A PARAMETERLESS COMMAND INSTEAD OF LOOPING THE SINGLE ONE ON THE CLIENT?
/// Marking each notification with its own request would be N round-trips and N transactions for a common
/// action. A dedicated bulk command does it in ONE server round-trip and ONE transaction — correct,
/// atomic, and cheap. It still funnels through MediatR so the same validation/logging pipeline applies.
/// </remarks>
public sealed record MarkAllNotificationsAsReadCommand : ICommand;
