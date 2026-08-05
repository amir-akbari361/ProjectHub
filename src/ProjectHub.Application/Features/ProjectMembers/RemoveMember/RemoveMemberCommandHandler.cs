using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Application.Common;
using ProjectHub.Domain.Enums;
using ProjectHub.Domain.Exceptions;

namespace ProjectHub.Application.Features.ProjectMembers.RemoveMember;

/// <summary>
/// Handles <see cref="RemoveMemberCommand"/>. A WRITE-side handler that loads the <c>Project</c> aggregate
/// WITH its members (needed for the caller's authorization AND the domain's last-owner invariant),
/// authorizes the caller, then delegates the removal to the domain's <c>RemoveMember</c> behavior and
/// commits once.
/// </summary>
/// <remarks>
/// AUTHORIZATION MODEL:
/// Two kinds of caller may remove a member: (a) a Maintainer/Owner managing the roster, or (b) any member
/// removing THEMSELVES (voluntarily leaving). Removing SOMEONE ELSE requires Maintainer/Owner. Removing an
/// Owner — even yourself — is reserved to Owners, mirroring the ChangeMemberRole owner-tier rule so a
/// Maintainer can never strip an Owner. The domain still owns the "last owner cannot be removed" invariant.
/// </remarks>
public sealed class RemoveMemberCommandHandler : ICommandHandler<RemoveMemberCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RemoveMemberCommandHandler> _logger;

    public RemoveMemberCommandHandler(
        IApplicationDbContext context,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork,
        ILogger<RemoveMemberCommandHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> Handle(
        RemoveMemberCommand request, CancellationToken cancellationToken)
    {
        // 1. Resolve the caller. Authorized + attributed action => 401 before any DB access.
        if (_currentUser.UserId is not { } callerId)
        {
            _logger.LogWarning("RemoveMember reached the handler without an authenticated user.");
            return Result.Failure(Error.Unauthorized(
                "Members.Unauthenticated",
                "You must be signed in to manage project members."));
        }

        // 2. Load the aggregate WITH its members, tracked. Include is required: both the authorization
        //    read and the domain's last-owner invariant walk the member collection, and tracking lets EF
        //    delete the child row on SaveChanges. Soft-deleted projects are hidden by the global filter.
        var project = await _context.Projects
            .Include(p => p.Members)
            .SingleOrDefaultAsync(p => p.Id == request.ProjectId, cancellationToken);

        // 3. Collapse "unknown project" and "caller is not a member" into one NotFound — no disclosure.
        var callerMembership = project?.Members.SingleOrDefault(m => m.UserId == callerId);
        if (project is null || callerMembership is null)
        {
            _logger.LogInformation(
                "RemoveMember: project {ProjectId} not found or not visible to user {UserId}.",
                request.ProjectId, callerId);
            return Result.Failure(MemberErrors.ProjectNotFound(request.ProjectId));
        }

        // 4. Authorize. Removing SOMEONE ELSE requires Maintainer/Owner; removing YOURSELF is always allowed
        //    (subject to the owner-tier and last-owner checks below).
        var isSelfRemoval = request.UserId == callerId;
        var callerCanManage = callerMembership.Role is (ProjectRole.Maintainer or ProjectRole.Owner);
        if (!isSelfRemoval && !callerCanManage)
        {
            _logger.LogInformation(
                "RemoveMember: user {UserId} with role {Role} may not remove others from project {ProjectId}.",
                callerId, callerMembership.Role, request.ProjectId);
            return Result.Failure(MemberErrors.Forbidden);
        }

        // 5. Owner-tier guard. Removing an Owner — even oneself — is reserved to Owners, so a Maintainer can
        //    never strip an Owner. The target's current role is read from the loaded collection; a missing
        //    target is left to the domain, which raises the precise "not a member" message.
        var targetMembership = project.Members.SingleOrDefault(m => m.UserId == request.UserId);
        if (targetMembership?.Role == ProjectRole.Owner && callerMembership.Role != ProjectRole.Owner)
        {
            _logger.LogInformation(
                "RemoveMember: user {UserId} attempted to remove an owner from project {ProjectId}.",
                callerId, request.ProjectId);
            return Result.Failure(MemberErrors.OwnerOnly);
        }

        // 6. Delegate to the domain. RemoveMember enforces "is a member" and "the last owner cannot be
        //    removed", stamps UpdatedBy/UpdatedAt, and raises the removed event. A DomainException is an
        //    EXPECTED business collision => surface as a modeled 409, not a 500.
        var utcNow = _dateTimeProvider.UtcNow;

        try
        {
            project.RemoveMember(request.UserId, utcNow, callerId);
        }
        catch (DomainException exception)
        {
            _logger.LogInformation(
                exception, "RemoveMember rejected by domain invariant for project {ProjectId}.",
                request.ProjectId);
            return Result.Failure(MemberErrors.Conflict(exception.Message));
        }

        // 7. Commit. The aggregate is tracked, so removing the child from the collection is translated to a
        //    DELETE; the domain event is dispatched in the same transaction by the interceptor.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "User {TargetUserId} removed from project {ProjectId} by {CallerId}.",
            request.UserId, request.ProjectId, callerId);

        return Result.Success();
    }
}
