using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectHub.Application.Common;
using ProjectHub.Application.Features.Admin.Users.ListUsers;
using ProjectHub.Application.Features.Admin.Users.ManageRoles;
using ProjectHub.Application.Features.Admin.Users.SetUserStatus;

namespace ProjectHub.API.Controllers;

/// <summary>
/// The HTTP entry point for user administration: list accounts, switch them on and off, and grant or revoke
/// global roles. A thin adapter like every other controller — bind, dispatch through MediatR, hand the
/// <c>Result</c> to <see cref="ApiController.HandleResult"/>.
/// </summary>
/// <remarks>
/// The <c>Admin</c> policy is applied to the CLASS, not per action, so an action added later inherits the gate
/// instead of being accidentally public. The handlers additionally re-check the role, so the authorisation
/// survives being reached by anything other than this controller.
///
/// WHY POST FOR THE STATUS CHANGES RATHER THAN PATCH?
/// "Deactivate" and "activate" are named transitions with their own rules, not arbitrary edits to an
/// <c>isActive</c> field — the same verb-sub-resource convention this codebase already uses for marking a
/// notification read and assigning a task. It also keeps the surface honest: there is deliberately no endpoint
/// that lets a client PATCH whatever user property it likes.
/// </remarks>
[Authorize(Policy = "Admin")]
[Route("api/admin/users")]
public sealed class AdminUsersController : ApiController
{
    private readonly ISender _sender;

    public AdminUsersController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Lists every account, alphabetically by email, as a paged envelope. Supports an optional text search
    /// across email/first/last name and an optional active-status filter.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedList<AdminUserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] ListUsersRequest request,
        CancellationToken cancellationToken)
    {
        var query = new ListUsersQuery(
            request.SearchTerm, request.IsActive, request.PageNumber, request.PageSize);

        var result = await _sender.Send(query, cancellationToken);

        return HandleResult(result);
    }

    /// <summary>
    /// Switches an account off so its holder can no longer sign in. <c>204</c> on success, <c>404</c> if the
    /// user is unknown, or <c>409</c> if it is already inactive or is the caller's own account.
    /// </summary>
    [HttpPost("{id:guid}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new DeactivateUserCommand(id), cancellationToken);

        return HandleResult(result);
    }

    /// <summary>
    /// Restores a deactivated account. <c>204</c> on success, <c>404</c> if the user is unknown, or
    /// <c>409</c> if it is already active.
    /// </summary>
    [HttpPost("{id:guid}/activate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ReactivateUserCommand(id), cancellationToken);

        return HandleResult(result);
    }

    /// <summary>
    /// Grants a global role to an account. <c>204</c> on success, <c>404</c> if the user or role is unknown,
    /// or <c>409</c> if the user already holds it.
    /// </summary>
    [HttpPost("{id:guid}/roles")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AssignRole(
        Guid id,
        AssignRoleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new AssignUserRoleCommand(id, request.RoleName), cancellationToken);

        return HandleResult(result);
    }

    /// <summary>
    /// Revokes a global role from an account. The role travels in the ROUTE because a DELETE identifies the
    /// resource being removed rather than carrying a body. <c>204</c> on success, <c>404</c> if the user or
    /// role is unknown, or <c>409</c> if the user does not hold it — or if it is the caller's own Admin role.
    /// </summary>
    [HttpDelete("{id:guid}/roles/{roleName}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveRole(
        Guid id,
        string roleName,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new RemoveUserRoleCommand(id, roleName), cancellationToken);

        return HandleResult(result);
    }
}

/// <summary>
/// Query-string binding target for <see cref="AdminUsersController.List"/>, mirroring the query's filters and
/// defaults so a bare <c>GET</c> is valid.
/// </summary>
public sealed record ListUsersRequest(
    string? SearchTerm = null,
    bool? IsActive = null,
    int PageNumber = 1,
    int PageSize = 20);

/// <summary>Body of <see cref="AdminUsersController.AssignRole"/> — the global role name to grant.</summary>
public sealed record AssignRoleRequest(string RoleName);
