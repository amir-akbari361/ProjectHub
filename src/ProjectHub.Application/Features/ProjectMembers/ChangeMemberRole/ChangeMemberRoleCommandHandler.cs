using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Application.Common;
using ProjectHub.Domain.Enums;
using ProjectHub.Domain.Exceptions;

namespace ProjectHub.Application.Features.ProjectMembers.ChangeMemberRole;

/// <summary>
/// Handles <see cref="ChangeMemberRoleCommand"/>. A WRITE-side handler that loads the <c>Project</c>
/// aggregate WITH its members (needed for the caller's authorization AND the domain's last-owner
/// invariant), authorizes the caller, then delegates the mutation to the domain's <c>ChangeMemberRole</c>
/// behavior and commits once.
/// </summary>
/// <remarks>
/// WHY THE OWNER-ONLY GUARD ON BOTH SIDES?
/// Changing a role can escalate (→ Owner) OR de-escalate (Owner → something lesser). Both touch the
/// Owner tier, so both are reserved to existing Owners: a Maintainer may reshuffle Viewer/Contributor/
/// Maintainer roles, but may neither MINT a new Owner nor STRIP an existing one. The domain still owns
/// the "at least one owner must remain" invariant — this handler only decides WHO is allowed to try.
/// </remarks>
public sealed class ChangeMemberRoleCommandHandler : ICommandHandler<ChangeMemberRoleCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ChangeMemberRoleCommandHandler> _logger;

    public ChangeMemberRoleCommandHandler(
        IApplicationDbContext context,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork,
        ILogger<ChangeMemberRoleCommandHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> Handle(
        ChangeMemberRoleCommand request, CancellationToken cancellationToken)
    {
        // 1. Resolve the caller. Authorized + attributed action => 401 before any DB access.
        if (_currentUser.UserId is not { } callerId)
        {
            _logger.LogWarning("ChangeMemberRole reached the handler without an authenticated user.");
            return Result.Failure(Error.Unauthorized(
                "Members.Unauthenticated",
                "You must be signed in to manage project members."));
        }

        // 2. Load the aggregate WITH its members, tracked. Include is required: both the authorization
        //    read and the domain's last-owner invariant walk the member collection, and tracking lets EF
        //    detect the role change on SaveChanges. Soft-deleted projects are hidden by the global filter.
        var project = await _context.Projects
            .Include(p => p.Members)
            .SingleOrDefaultAsync(p => p.Id == request.ProjectId, cancellationToken);

        // 3. Collapse "unknown project" and "caller is not a member" into one NotFound — no disclosure.
        var callerMembership = project?.Members.SingleOrDefault(m => m.UserId == callerId);
        if (project is null || callerMembership is null)
        {
            _logger.LogInformation(
                "ChangeMemberRole: project {ProjectId} not found or not visible to user {UserId}.",
                request.ProjectId, callerId);
            return Result.Failure(MemberErrors.ProjectNotFound(request.ProjectId));
        }

        // 4. Authorize. Managing members requires Maintainer or Owner. Caller IS a member => 403, not 404.
        if (callerMembership.Role is not (ProjectRole.Maintainer or ProjectRole.Owner))
        {
            _logger.LogInformation(
                "ChangeMemberRole: user {UserId} with role {Role} may not manage members of project {ProjectId}.",
                callerId, callerMembership.Role, request.ProjectId);
            return Result.Failure(MemberErrors.Forbidden);
        }

        // 5. Owner-tier guard. Both granting Owner and revoking an existing Owner are reserved to Owners.
        //    We resolve the TARGET's current role from the loaded collection to know if we're de-escalating
        //    an Owner. A missing target here is left to the domain, which raises the precise "not a member".
        var targetMembership = project.Members.SingleOrDefault(m => m.UserId == request.UserId);
        var touchesOwnerTier =
            request.NewRole == ProjectRole.Owner ||
            targetMembership?.Role == ProjectRole.Owner;

        if (touchesOwnerTier && callerMembership.Role != ProjectRole.Owner)
        {
            _logger.LogInformation(
                "ChangeMemberRole: maintainer {UserId} attempted an owner-tier change in project {ProjectId}.",
                callerId, request.ProjectId);
            return Result.Failure(MemberErrors.OwnerOnly);
        }

        // 6. Delegate to the domain. ChangeMemberRole enforces "is a member", "role actually changes", and
        //    "at least one owner remains", stamps UpdatedBy/UpdatedAt, and raises the role-changed event. A
        //    DomainException is an EXPECTED business collision => surface as a modeled 409, not a 500.
        var utcNow = _dateTimeProvider.UtcNow;

        try
        {
            project.ChangeMemberRole(request.UserId, request.NewRole, utcNow, callerId);
        }
        catch (DomainException exception)
        {
            _logger.LogInformation(
                exception, "ChangeMemberRole rejected by domain invariant for project {ProjectId}.",
                request.ProjectId);
            return Result.Failure(MemberErrors.Conflict(exception.Message));
        }

        // 7. Commit. The aggregate is tracked, so one SaveChanges flushes the update; the domain event is
        //    dispatched in the same transaction by the interceptor.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Role of user {TargetUserId} in project {ProjectId} changed to {Role} by {CallerId}.",
            request.UserId, request.ProjectId, request.NewRole, callerId);

        return Result.Success();
    }
}
