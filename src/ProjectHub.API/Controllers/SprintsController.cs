using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectHub.Application.Features.Sprints.CompleteSprint;
using ProjectHub.Application.Features.Sprints.CreateSprint;
using ProjectHub.Application.Features.Sprints.ListSprints;
using ProjectHub.Application.Features.Sprints.StartSprint;

namespace ProjectHub.API.Controllers;

/// <summary>
/// The HTTP entry point for the sprint use cases. Like <see cref="TasksController"/> every action is a
/// THIN adapter: it stitches the route/body into a command or query, dispatches it through MediatR, and
/// hands the resulting <c>Result</c> to <see cref="ApiController.HandleResult"/> for HTTP mapping. No
/// business logic lives here — authorization, membership checks, and the sprint lifecycle invariants are
/// all enforced in the Application handlers and the <c>Sprint</c> aggregate.
/// </summary>
/// <remarks>
/// WHY TWO ROUTE SHAPES IN ONE CONTROLLER?
/// A sprint is a CHILD of a project, so the collection-level operations (create, list) read most naturally
/// as sub-resources of a project: <c>/api/projects/{projectId}/sprints</c>. But once a sprint exists it has
/// its own stable identity, so the item-level lifecycle actions (start, complete) hang off
/// <c>/api/sprints/{id}</c> — a client holding a sprint id shouldn't need to know its project to act on it.
/// We express the project-scoped routes with absolute templates ("~/...") so they escape the controller's
/// default <c>api/[controller]</c> prefix, and leave the item routes to inherit it. This mirrors
/// <see cref="TasksController"/> exactly.
///
/// Every action is <c>[Authorize]</c> (secure by default): sprints are always acted on BY a known member
/// and attributed to them, so a request without a valid token fails closed with 401.
/// </remarks>
[Authorize]
public sealed class SprintsController : ApiController
{
    private readonly ISender _sender;

    public SprintsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Creates a new sprint inside a project. The project id comes from the ROUTE and the sprint fields
    /// from the BODY; we stitch them into the command so the parent identity can never be spoofed by a
    /// mismatched body field. Returns <c>201 Created</c> with the new sprint's id and normalized name, and
    /// a <c>Location</c> header pointing at the project's sprint collection (there is no item-level GET in
    /// this slice, so the collection is the closest addressable resource).
    /// </summary>
    [HttpPost("~/api/projects/{projectId:guid}/sprints")]
    [ProducesResponseType(typeof(CreateSprintResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        Guid projectId,
        CreateSprintRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateSprintCommand(
            projectId, request.Name, request.StartUtc, request.EndUtc);

        var result = await _sender.Send(command, cancellationToken);

        return HandleResult(result, value => CreatedAtAction(
            actionName: nameof(List),
            routeValues: new { projectId },
            value: value));
    }

    /// <summary>
    /// Lists a project's sprints, ordered by start date. Returns <c>200 OK</c> with a flat list (sprints
    /// per project are few, so this is not paged), or <c>404</c> if the project is unknown OR the caller is
    /// not a member (the two are deliberately indistinguishable to avoid information disclosure).
    /// </summary>
    [HttpGet("~/api/projects/{projectId:guid}/sprints")]
    [ProducesResponseType(typeof(IReadOnlyList<SprintResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ListSprintsQuery(projectId), cancellationToken);

        return HandleResult(result);
    }

    /// <summary>
    /// Starts a sprint (Planned → Active). Returns <c>204 No Content</c> on success, <c>403</c> if the
    /// caller lacks a mutating role, <c>404</c> if the sprint is unknown/invisible, or <c>409</c> if the
    /// sprint is not currently Planned.
    /// </summary>
    /// <remarks>
    /// WHY <c>POST .../start</c> AND NOT <c>PUT</c>? Starting is a discrete named lifecycle action against
    /// an existing resource, not a wholesale replacement of the sprint representation, so a verb
    /// sub-resource communicates intent more precisely than PUT on the sprint itself.
    /// </remarks>
    [HttpPost("{id:guid}/start")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Start(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new StartSprintCommand(id), cancellationToken);

        return HandleResult(result);
    }

    /// <summary>
    /// Completes a sprint (Active → Completed). Returns <c>204 No Content</c> on success, <c>403</c> if the
    /// caller lacks a mutating role, <c>404</c> if the sprint is unknown/invisible, or <c>409</c> if the
    /// sprint is not currently Active.
    /// </summary>
    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Complete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CompleteSprintCommand(id), cancellationToken);

        return HandleResult(result);
    }
}

/// <summary>
/// Request body for <see cref="SprintsController.Create"/>. Carries ONLY client-supplied fields; the parent
/// project id is owned by the route and stitched in by the controller, so it can never be double-sourced.
/// The two boundaries are plain <see cref="DateTime"/>s — the handler coerces them to UTC before building
/// the domain <c>DateRange</c>.
/// </summary>
public sealed record CreateSprintRequest(string Name, DateTime StartUtc, DateTime EndUtc);
