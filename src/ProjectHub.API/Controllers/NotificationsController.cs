using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectHub.Application.Common;
using ProjectHub.Application.Features.Notifications.ListNotifications;
using ProjectHub.Application.Features.Notifications.MarkAllAsRead;
using ProjectHub.Application.Features.Notifications.MarkAsRead;

namespace ProjectHub.API.Controllers;

/// <summary>
/// The HTTP entry point for the current user's notification inbox. Like every other controller in this
/// solution each action is a THIN adapter: it stitches route/query into a command or query, dispatches
/// through MediatR, and hands the <c>Result</c> to <see cref="ApiController.HandleResult"/>. No business
/// logic lives here — the recipient is always resolved from the token in the handler, so ownership is
/// enforced in the Application layer, never trusted from the client.
/// </summary>
/// <remarks>
/// WHY IS THERE NO RECIPIENT ANYWHERE IN THESE ROUTES?
/// Every endpoint operates on the AUTHENTICATED caller's own inbox. There is deliberately no
/// <c>/users/{id}/notifications</c> shape — exposing another user's id would invite IDOR. The identity
/// comes from the JWT, so the routes stay flat: <c>/api/notifications</c> and its sub-resources.
///
/// Every action is <c>[Authorize]</c> (secure by default): an inbox is meaningless without a known user, so
/// a request lacking a valid token fails closed with 401.
/// </remarks>
[Authorize]
public sealed class NotificationsController : ApiController
{
    private readonly ISender _sender;

    public NotificationsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Lists the caller's notifications as a single page, newest-first. Paging and the optional
    /// <c>unreadOnly</c> filter arrive as QUERY-STRING parameters. Returns <c>200 OK</c> with a
    /// <see cref="PagedList{T}"/> envelope.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedList<NotificationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(
        [FromQuery] ListNotificationsRequest request,
        CancellationToken cancellationToken)
    {
        var query = new ListNotificationsQuery(
            request.UnreadOnly, request.PageNumber, request.PageSize);

        var result = await _sender.Send(query, cancellationToken);

        return HandleResult(result);
    }

    /// <summary>
    /// Marks a single notification — identified by the route id — as read. Returns <c>204 No Content</c>
    /// on success (idempotent: already-read is still a 204), or <c>404</c> if the notification is unknown
    /// or belongs to another user.
    /// </summary>
    /// <remarks>
    /// WHY <c>POST .../read</c> AND NOT <c>PUT</c>? "Mark as read" is a named state transition, not a
    /// whole-representation replacement, so it reads as an action sub-resource — the same convention this
    /// codebase uses for task assignment/status. It is naturally idempotent, which POST permits.
    /// </remarks>
    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsRead(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new MarkNotificationAsReadCommand(id), cancellationToken);

        return HandleResult(result);
    }

    /// <summary>
    /// Marks EVERY unread notification of the caller as read in one shot — the "clear the badge" action.
    /// Returns <c>204 No Content</c> on success (idempotent: an already-empty inbox is still a 204).
    /// </summary>
    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new MarkAllNotificationsAsReadCommand(), cancellationToken);

        return HandleResult(result);
    }
}

/// <summary>
/// Query-string binding target for <see cref="NotificationsController.List"/>. Mirrors the paging and
/// filter concerns of <c>ListNotificationsQuery</c> WITHOUT the recipient (owned by the token). Defaults
/// match the query's own defaults so a bare <c>GET</c> is valid.
/// </summary>
public sealed record ListNotificationsRequest(
    int PageNumber = 1,
    int PageSize = 20,
    bool UnreadOnly = false);
