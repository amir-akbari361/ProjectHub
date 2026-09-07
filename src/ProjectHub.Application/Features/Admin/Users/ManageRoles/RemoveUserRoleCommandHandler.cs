using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Application.Common;
using ProjectHub.Domain.Entities;

namespace ProjectHub.Application.Features.Admin.Users.ManageRoles;

/// <summary>
/// Handles <see cref="RemoveUserRoleCommand"/>. Resolves the role by name, loads the target account with its
/// assignments, blocks the self-lockout case, rejects a missing assignment with a modelled conflict, then
/// delegates to <c>User.RemoveRole</c> and commits once.
/// </summary>
/// <remarks>
/// WHY THE SELF-LOCKOUT GUARD IS SPECIFIC TO THE ADMIN ROLE
/// Dropping your own Manager or Member role costs you nothing you cannot restore — this console is gated on
/// Admin. Dropping your own ADMIN role locks the door behind you: the next token refresh no longer carries the
/// claim, and the page you would use to grant it back is itself Admin-only. Another administrator can still
/// revoke it, so the privilege remains removable; it just cannot be surrendered by a misclick.
///
/// WHY COMPARE THE ROLE NAME CASE-INSENSITIVELY
/// The name arrives from a client and the database collation is case-insensitive, so <c>"admin"</c> resolves
/// to the Admin role. If this guard compared exactly, that same request would slip past it — the check must be
/// at least as lenient as the lookup that produced the role.
/// </remarks>
public sealed class RemoveUserRoleCommandHandler : ICommandHandler<RemoveUserRoleCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RemoveUserRoleCommandHandler> _logger;

    public RemoveUserRoleCommandHandler(
        IApplicationDbContext context,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork,
        ILogger<RemoveUserRoleCommandHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> Handle(RemoveUserRoleCommand request, CancellationToken cancellationToken)
    {
        var access = AdminAccess.Resolve(_currentUser);

        if (access.IsFailure)
        {
            _logger.LogWarning("RemoveUserRole was refused: {ErrorCode}.", access.Error.Code);
            return Result.Failure(access.Error);
        }

        var roleName = request.RoleName.Trim();

        var role = await _context.Roles
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Name == roleName, cancellationToken);

        if (role is null)
        {
            return Result.Failure(AdminUserErrors.RoleNotFound(roleName));
        }

        var isOwnAccount = request.UserId == access.Value;
        var isAdminRole = string.Equals(role.Name, Role.Admin.Name, StringComparison.OrdinalIgnoreCase);

        if (isOwnAccount && isAdminRole)
        {
            _logger.LogWarning(
                "Admin {AdminId} attempted to remove their own Admin role.", access.Value);
            return Result.Failure(AdminUserErrors.CannotRemoveOwnAdminRole);
        }

        var user = await _context.Users
            .Include(candidate => candidate.Roles)
            .SingleOrDefaultAsync(candidate => candidate.Id == request.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(AdminUserErrors.UserNotFound(request.UserId));
        }

        // Pre-checked for the same reason as the grant: a modelled 409 naming the role beats the domain's
        // exception for a case the console can hit simply by double-clicking.
        if (user.Roles.All(assignment => assignment.RoleId != role.Id))
        {
            return Result.Failure(AdminUserErrors.RoleNotAssigned(role.Name));
        }

        user.RemoveRole(role, _dateTimeProvider.UtcNow, access.Value);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Admin {AdminId} revoked role {RoleName} from user {UserId}.",
            access.Value, role.Name, request.UserId);

        return Result.Success();
    }
}
