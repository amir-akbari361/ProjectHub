using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectHub.Application.Common;
using ProjectHub.Application.Features.Projects.ArchiveProject;
using ProjectHub.Application.Features.Projects.CreateProject;
using ProjectHub.Application.Features.Projects.GetProjectById;
using ProjectHub.Application.Features.Projects.ListProjects;
using ProjectHub.Application.Features.Projects.UpdateProject;

namespace ProjectHub.API.Controllers;

/// <summary>
/// The HTTP entry point for the project management use cases. Like <see cref="AuthController"/> every
/// action is a THIN adapter: it binds the request to a command/query, dispatches it through MediatR,
/// and hands the resulting <c>Result</c> to <see cref="ApiController.HandleResult"/> for HTTP mapping.
/// No business logic lives here — that all sits in the Application handlers.
/// </summary>
/// <remarks>
/// WHY <c>[Authorize]</c> AT THE CLASS LEVEL (the opposite of AuthController's <c>[AllowAnonymous]</c>)?
/// Every project operation is performed BY a known user and attributed to them (CreatedBy, owner, audit
/// trail). A caller without a valid access token has no identity to attribute, so we deny by default at
/// the controller level and opt specific endpoints OUT with <c>[AllowAnonymous]</c> if ever needed. This
/// "secure by default" posture means forgetting an attribute fails CLOSED (401), never open.
/// </remarks>
[Authorize]
public sealed class ProjectsController : ApiController
{
    private readonly ISender _sender;

    public ProjectsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Creates a new project owned by the caller. Returns <c>201 Created</c> with the new project's
    /// id and normalized name — <c>201</c> (not <c>200</c>) because a resource was created, and a
    /// <c>Location</c> header pointing at the future GET endpoint so clients can follow REST discovery.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreateProjectResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create(
        CreateProjectCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);

        // Override the default 200 → 201 Created and emit a Location header. We use CreatedAtAction with
        // a nameof reference so the URL stays correct even if the route template changes; the GetById
        // action doesn't exist yet, so this is wired to its intended name for when it lands next.
        return HandleResult(result, value => CreatedAtAction(
            actionName: nameof(GetById),
            routeValues: new { id = value.Id },
            value: value));
    }

    /// <summary>
    /// Fetches a single project the caller belongs to. Returns <c>200 OK</c> with the projection, or
    /// <c>404 Not Found</c> if the id is unknown OR the caller is not a member (the two are deliberately
    /// indistinguishable — see the handler's remarks on avoiding information disclosure).
    /// </summary>
    /// <remarks>
    /// WHY THE ROUTE IS <c>{id:guid}</c>? Constraining the segment to a GUID means a non-GUID like
    /// <c>/api/projects/abc</c> never reaches this action (the router 404s it), so the id we bind is
    /// already well-formed. The action name matches the <c>nameof(GetById)</c> reference the Create
    /// action uses to build its <c>Location</c> header, so the two stay in sync automatically.
    /// </remarks>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProjectResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetProjectByIdQuery(id), cancellationToken);

        return HandleResult(result);
    }

    /// <summary>
    /// Lists the projects the caller belongs to as a single page. Pagination, filtering, and sorting
    /// arrive as QUERY-STRING parameters (<c>?pageNumber=&amp;pageSize=&amp;searchTerm=&amp;status=&amp;sortBy=&amp;sortDescending=</c>)
    /// because they refine a GET on a collection — they are not a request BODY. Returns <c>200 OK</c>
    /// with a <see cref="PagedList{T}"/> envelope of items plus paging metadata.
    /// </summary>
    /// <remarks>
    /// WHY <c>[FromQuery]</c> AND NOT A BODY? A GET must be safe and cacheable and, by spec, carries no
    /// semantic body. Binding the criteria from the query string keeps the endpoint a pure, bookmarkable
    /// read. Model binding maps each query key onto the record's properties; omitted keys fall back to
    /// the record's own defaults (page 1, size 20), so <c>GET /api/projects</c> alone is valid.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(PagedList<ProjectListItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(
        [FromQuery] ListProjectsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);

        return HandleResult(result);
    }

    /// <summary>
    /// Updates a project's name and description. The id comes from the ROUTE and the new values from the
    /// BODY; we stitch them together into the command so the resource identity can never be spoofed by a
    /// mismatched body field. Returns <c>204 No Content</c> on success (the client already knows what it
    /// sent), <c>403</c> if the caller lacks the Maintainer/Owner role, or <c>404</c> if it isn't a member.
    /// </summary>
    /// <remarks>
    /// WHY <c>PUT</c> AND WHY REBUILD THE COMMAND? PUT is the idempotent "replace the mutable fields"
    /// verb — sending the same body twice leaves the same state. We construct a NEW command from the
    /// route id plus the body rather than trusting a body-supplied id, closing the door on a request that
    /// targets <c>/projects/A</c> but smuggles <c>id: B</c> in its payload.
    /// </remarks>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateProjectRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateProjectCommand(id, request.Name, request.Description);

        var result = await _sender.Send(command, cancellationToken);

        return HandleResult(result);
    }

    /// <summary>
    /// Archives (retires) a project, taking its board read-only. Owner-only. Returns <c>204 No Content</c>
    /// on success, <c>409 Conflict</c> if the project is already archived, <c>403</c> for non-Owners, or
    /// <c>404</c> if the caller isn't a member.
    /// </summary>
    /// <remarks>
    /// WHY <c>POST .../archive</c> AND NOT <c>DELETE</c>? Archiving is a STATE TRANSITION, not a deletion —
    /// the project still exists and can be read. Modeling it as a named sub-resource action ("archive")
    /// communicates that intent precisely, whereas <c>DELETE</c> would wrongly imply the row is gone. This
    /// is the standard REST pattern for verbs that don't map cleanly onto CRUD.
    /// </remarks>
    [HttpPost("{id:guid}/archive")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Archive(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ArchiveProjectCommand(id), cancellationToken);

        return HandleResult(result);
    }
}

/// <summary>
/// The request body for <see cref="ProjectsController.Update"/>. It carries ONLY the client-supplied
/// values (name, description); the project id is intentionally absent because it is owned by the route.
/// Keeping this separate from <c>UpdateProjectCommand</c> means the wire contract and the internal
/// command can evolve independently, and the id can never be double-sourced.
/// </summary>
public sealed record UpdateProjectRequest(string Name, string? Description);


