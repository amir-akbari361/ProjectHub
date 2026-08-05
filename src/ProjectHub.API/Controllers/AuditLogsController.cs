using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectHub.Application.Common;
using ProjectHub.Application.Features.AuditLogs.ListAuditLogs;

namespace ProjectHub.API.Controllers;

/// <summary>
/// The HTTP entry point for reading an entity's immutable audit trail. Like every controller in this
/// solution each action is a THIN adapter: it binds route/query values into a query, dispatches through
/// MediatR, and hands the <c>Result</c> to <see cref="ApiController.HandleResult"/>. No business logic
/// lives here — the audit store is read-only, so this controller exposes only a GET.
/// </summary>
/// <remarks>
/// WHY IS THE ROUTE SHAPED AROUND (entityName, entityId)?
/// An audit trail is always "the history OF something." Modeling the target as two path segments —
/// <c>/api/auditlogs/{entityName}/{entityId}</c> — reads naturally and keys directly onto the composite
/// (EntityName, EntityId) the writers use. Paging arrives as the conventional query string so a bare
/// call returns the first page. Every action is <c>[Authorize]</c> (secure by default): audit history is
/// an internal record, meaningless to an anonymous caller, so a missing token fails closed with 401.
/// </remarks>
[Authorize]
public sealed class AuditLogsController : ApiController
{
    private readonly ISender _sender;

    public AuditLogsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Returns the audit trail for one entity, newest-first, as a paged envelope. The entity name and id
    /// bind from the route; paging binds from the query string. Returns <c>200 OK</c> with a
    /// <see cref="PagedList{T}"/> of <see cref="AuditLogResponse"/>, or <c>400</c> when the entity name is
    /// not an audited type / paging is out of range.
    /// </summary>
    [HttpGet("{entityName}/{entityId:guid}")]
    [ProducesResponseType(typeof(PagedList<AuditLogResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(
        string entityName,
        Guid entityId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new ListAuditLogsQuery(entityName, entityId, pageNumber, pageSize);

        var result = await _sender.Send(query, cancellationToken);

        return HandleResult(result);
    }
}
