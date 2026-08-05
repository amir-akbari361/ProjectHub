using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectHub.Application.Features.ProjectMembers.AddMember;
using ProjectHub.Application.Features.ProjectMembers.ChangeMemberRole;
using ProjectHub.Application.Features.ProjectMembers.ListMembers;
using ProjectHub.Application.Features.ProjectMembers.RemoveMember;
using ProjectHub.Domain.Enums;

namespace ProjectHub.API.Controllers;

/// <summary>
/// The HTTP entry point for managing a project's MEMBER ROSTER. Every action is a THIN adapter: it stitches
/// route/body into a command or query, dispatches through MediatR, and hands the <c>Result</c> to
/// <see cref="ApiController.HandleResult"/>. No business logic lives here — visibility, the manage-members
/// role check, the owner-tier guard, and the last-owner invariant are enforced in the Application handlers
/// and the <c>Project</c> aggregate.
/// </summary>
/// <remarks>
/// WHY THIS ROUTE SHAPE?
/// A membership is a CHILD of a project, so the whole controller is nested under a project:
/// <c>/api/projects/{projectId}/members</c>. A single membership is addressed by the USER's id
/// (<c>.../members/{userId}</c>) because that — not a surrogate membership id — is what a client naturally
/// holds when it says "change this person's role" or "remove this person". The <c>[controller]</c> token is
/// unused here; we spell the template out so the project prefix is explicit.
///
/// Every action is <c>[Authorize]</c> (secure by default): the roster is scoped to project membership, so a
/// request without a valid token fails closed with 401.
/// </remarks>
[Authorize]
[Route("api/projects/{projectId:guid}/members")]
public sealed class ProjectMembersController : ApiController
{
    private readonly ISender _sender;

    public ProjectMembersController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Lists the project's full member roster (Owner→Maintainer→… order), each row enriched with the
    /// member's email and full name. Returns <c>200 OK</c>. Not paged — a roster is a small, bounded set.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<MemberResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List(Guid projectId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ListMembersQuery(projectId), cancellationToken);

        return HandleResult(result);
    }

    /// <summary>
    /// Adds a user to the project with an initial role. The project id comes from the ROUTE, the user id
    /// and role from the body; the caller (who must be a Maintainer/Owner) is resolved from the token in
    /// the handler. Returns <c>201 Created</c> with the new membership, and a <c>Location</c> header
    /// pointing at the roster.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(AddMemberResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Add(
        Guid projectId,
        AddMemberRequest request,
        CancellationToken cancellationToken)
    {
        var command = new AddMemberCommand(projectId, request.UserId, request.Role);

        var result = await _sender.Send(command, cancellationToken);

        return HandleResult(result, value => CreatedAtAction(
            actionName: nameof(List),
            routeValues: new { projectId },
            value: value));
    }

    /// <summary>
    /// Changes an existing member's role. Both ids come from the ROUTE, the new role from the body; the
    /// caller is resolved from the token. Returns <c>204 No Content</c> on success. The owner-tier and
    /// last-owner rules are enforced downstream.
    /// </summary>
    /// <remarks>
    /// WHY <c>PUT</c>? A member has exactly one role, so changing it REPLACES that single-valued
    /// sub-resource — the idempotent, whole-representation semantics of PUT fit precisely.
    /// </remarks>
    [HttpPut("{userId:guid}/role")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeRole(
        Guid projectId,
        Guid userId,
        ChangeMemberRoleRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ChangeMemberRoleCommand(projectId, userId, request.Role);

        var result = await _sender.Send(command, cancellationToken);

        return HandleResult(result);
    }

    /// <summary>
    /// Removes a member from the project. Both ids come from the ROUTE; the caller is resolved from the
    /// token. A member may remove themselves (leave) or, as a Maintainer/Owner, remove others. Returns
    /// <c>204 No Content</c> on success. Removing an owner is owner-only, and the last owner cannot be
    /// removed — both enforced downstream.
    /// </summary>
    [HttpDelete("{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Remove(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new RemoveMemberCommand(projectId, userId), cancellationToken);

        return HandleResult(result);
    }
}

/// <summary>
/// Request body for <see cref="ProjectMembersController.Add"/>. Carries the user to add and their initial
/// role; the project id is owned by the route and the caller by the token, so neither can be spoofed.
/// </summary>
public sealed record AddMemberRequest(Guid UserId, ProjectRole Role);

/// <summary>
/// Request body for <see cref="ProjectMembersController.ChangeRole"/>. Carries ONLY the new role; both the
/// project and the target user are owned by the route.
/// </summary>
public sealed record ChangeMemberRoleRequest(ProjectRole Role);
