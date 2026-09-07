using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectHub.Application.Common;
using ProjectHub.Application.Features.AuditLogs.ListAllAuditLogs;

namespace ProjectHub.API.Controllers;

/// <summary>
/// The HTTP entry point for the organisation-wide audit trail. A thin adapter like every other controller:
/// bind the query string, dispatch through MediatR, hand the <c>Result</c> to
/// <see cref="ApiController.HandleResult"/>.
/// </summary>
/// <remarks>
/// WHY A SEPARATE CONTROLLER AND ROUTE INSTEAD OF ANOTHER ACTION ON <see cref="AuditLogsController"/>?
/// The two reads have different AUDIENCES and therefore different authorisation: that controller is
/// <c>[Authorize]</c> for any signed-in member and scopes by project membership, while this one is gated by
/// the <c>Admin</c> policy for the whole controller. Putting them side by side would mean one class with two
/// authorisation regimes — the arrangement in which someone eventually adds an action under the wrong
/// attribute. A distinct <c>api/admin/...</c> prefix also makes the privileged surface obvious in route
/// listings, logs, and any future gateway rule.
///
/// The base <c>[Route("api/[controller]")]</c> is overridden here because the admin grouping, not the class
/// name, should drive the URL.
/// </remarks>
[Authorize(Policy = "Admin")]
[Route("api/admin/audit-logs")]
public sealed class AdminAuditLogsController : ApiController
{
    private readonly ISender _sender;

    public AdminAuditLogsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Returns audit entries from across every project, newest-first, as a paged envelope. All filters are
    /// optional — a bare call returns the most recent activity organisation-wide. Returns <c>200 OK</c>,
    /// <c>400</c> when a filter or the paging window is malformed, <c>401</c> when unauthenticated, or
    /// <c>403</c> when the caller is not an Admin.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedList<AdminAuditLogResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] ListAllAuditLogsRequest request,
        CancellationToken cancellationToken)
    {
        var query = new ListAllAuditLogsQuery(
            request.EntityName,
            request.ProjectId,
            request.PerformedBy,
            request.FromUtc,
            request.ToUtc,
            request.PageNumber,
            request.PageSize);

        var result = await _sender.Send(query, cancellationToken);

        return HandleResult(result);
    }
}

/// <summary>
/// Query-string binding target for <see cref="AdminAuditLogsController.List"/>. Mirrors
/// <c>ListAllAuditLogsQuery</c>'s filters, with the same defaults so a bare <c>GET</c> is valid. Kept as a
/// separate type (rather than binding the query directly) so the wire contract can evolve independently of
/// the Application-layer query — the convention this codebase already follows for notifications.
/// </summary>
public sealed record ListAllAuditLogsRequest(
    string? EntityName = null,
    Guid? ProjectId = null,
    Guid? PerformedBy = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    int PageNumber = 1,
    int PageSize = 20);
