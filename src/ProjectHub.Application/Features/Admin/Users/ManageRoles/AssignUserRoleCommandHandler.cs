using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Application.Common;

namespace ProjectHub.Application.Features.Admin.Users.ManageRoles;

/// <summary>
/// Handles <see cref="AssignUserRoleCommand"/>. Resolves the role by name, loads the target account WITH its
/// existing assignments, rejects a duplicate with a modelled conflict, then delegates to
/// <c>User.AssignRole</c> and commits once.
/// </summary>
/// <remarks>
/// WHY <c>Include(u => u.Roles)</c> IS NOT OPTIONAL HERE
/// <c>User.AssignRole</c> enforces "no duplicate role" by inspecting its own in-memory collection. Loading the
/// user WITHOUT that collection would leave it empty, so the guard would pass, a second <c>UserRole</c> row
/// would be inserted, and the user would hold the same role twice — visible as a duplicated chip in the
/// console and a repeated claim in their next token. The Include is what makes the invariant real.
///
/// WHY THE ROLE IS READ WITH AsNoTracking
/// It is a lookup, not a participant in the change: only its id is used, to build the join row. Leaving it
/// untracked keeps the change set to exactly the user and its new assignment.
/// </remarks>
public sealed class AssignUserRoleCommandHandler : ICommandHandler<AssignUserRoleCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AssignUserRoleCommandHandler> _logger;

    public AssignUserRoleCommandHandler(
        IApplicationDbContext context,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork,
        ILogger<AssignUserRoleCommandHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> Handle(AssignUserRoleCommand request, CancellationToken cancellationToken)
    {
        var access = AdminAccess.Resolve(_currentUser);

        if (access.IsFailure)
        {
            _logger.LogWarning("AssignUserRole was refused: {ErrorCode}.", access.Error.Code);
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

        var user = await _context.Users
            .Include(candidate => candidate.Roles)
            .SingleOrDefaultAsync(candidate => candidate.Id == request.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(AdminUserErrors.UserNotFound(request.UserId));
        }

        // Pre-checked so a duplicate grant answers with a 409 that names the role, instead of the domain's
        // DomainException surfacing as an opaque server fault.
        if (user.Roles.Any(assignment => assignment.RoleId == role.Id))
        {
            return Result.Failure(AdminUserErrors.RoleAlreadyAssigned(role.Name));
        }

        user.AssignRole(role, _dateTimeProvider.UtcNow, access.Value);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Admin {AdminId} granted role {RoleName} to user {UserId}.",
            access.Value, role.Name, request.UserId);

        return Result.Success();
    }
}
