using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ProjectHub.API.Hubs;

/// <summary>
/// The live channel for a user's notification inbox. It is deliberately EMPTY of client-callable methods:
/// this hub is a one-way push surface (server → client), and every state-changing operation on a
/// notification already has an authorized, validated HTTP endpoint on <c>NotificationsController</c>.
/// Adding hub methods here would create a second, parallel API surface that bypasses the MediatR pipeline
/// (validation, logging, exception handling) — so mutations stay on HTTP and only fan-out lives here.
/// </summary>
/// <remarks>
/// <c>[Authorize]</c> is what makes per-user targeting possible at all: it guarantees every connection
/// carries an authenticated principal, from which <see cref="SubjectUserIdProvider"/> derives the user id
/// that <c>Clients.User(...)</c> addresses. An unauthenticated socket is rejected at the handshake, so a
/// connection can never be associated with the wrong inbox — or with no inbox.
///
/// Connections are NOT tracked here. SignalR's own user-to-connections mapping already handles a user
/// being open in several tabs or devices (all of them receive the push), so there is no group bookkeeping
/// and nothing to clean up on disconnect.
/// </remarks>
[Authorize]
public sealed class NotificationHub : Hub
{
    /// <summary>
    /// The client-side handler name invoked for each new notification. Named as a constant so the sender
    /// (<see cref="SignalRNotificationPusher"/>) cannot drift from what the Blazor client subscribes to —
    /// a mismatch here fails silently at runtime rather than at compile time.
    /// </summary>
    public const string NotificationReceived = "NotificationReceived";
}
