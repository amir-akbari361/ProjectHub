using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectHub.Application.Common;
using ProjectHub.Application.Features.Search.GlobalSearch;

namespace ProjectHub.API.Controllers;

/// <summary>
/// The HTTP entry point for cross-entity search. Like every controller in this solution each action is a
/// THIN adapter: it binds the query string into a query, dispatches through MediatR, and hands the
/// <c>Result</c> to <see cref="ApiController.HandleResult"/>. No business logic lives here — the searchable
/// scope is always resolved from the token in the handler, never trusted from the client.
/// </summary>
/// <remarks>
/// WHY IS THERE NO SCOPE PARAMETER IN THE ROUTE?
/// Search only ever spans what the AUTHENTICATED caller can see. There is deliberately no
/// <c>/users/{id}/search</c> shape — the identity comes from the JWT, so the route stays flat at
/// <c>/api/search</c>. Every action is <c>[Authorize]</c> (secure by default): search is meaningless
/// without a known user, so a request lacking a valid token fails closed with 401.
/// </remarks>
[Authorize]
public sealed class SearchController : ApiController
{
    private readonly ISender _sender;

    public SearchController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Searches across the caller's projects and their tasks, newest-first. The term, paging arrive as
    /// QUERY-STRING parameters. Returns <c>200 OK</c> with a <see cref="PagedList{T}"/> envelope of
    /// <see cref="SearchResultItem"/>, or <c>400</c> when the term is too short / paging out of range.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedList<SearchResultItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Search(
        [FromQuery] GlobalSearchRequest request,
        CancellationToken cancellationToken)
    {
        var query = new GlobalSearchQuery(
            request.Q, request.PageNumber, request.PageSize);

        var result = await _sender.Send(query, cancellationToken);

        return HandleResult(result);
    }
}

/// <summary>
/// Query-string binding target for <see cref="SearchController.Search"/>. Mirrors the term and paging of
/// <c>GlobalSearchQuery</c> WITHOUT the scope (owned by the token). The term binds from the conventional
/// <c>q</c> parameter (<c>/api/search?q=login</c>); paging defaults match the query's own defaults so a
/// call with only <c>q</c> is valid.
/// </summary>
public sealed record GlobalSearchRequest(
    string Q,
    int PageNumber = 1,
    int PageSize = 20);
