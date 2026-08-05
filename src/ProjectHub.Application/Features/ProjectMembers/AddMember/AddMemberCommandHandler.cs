using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Application.Common;
using ProjectHub.Domain.Entities;
using ProjectHub.Domain.Enums;
using ProjectHub.Domain.Exceptions;

namespace ProjectHub.Application.Features.ProjectMembers.AddMember;

/// <summary>
/// Handles <see cref="AddMemberCommand"/>. A WRITE-side handler that loads the <c>Project</c> aggregate
/// WITH its members (needed for both the caller's authorization and the domain's uniqueness/archived
/// invariants), authorizes the caller, verifies the target user exists, invokes the domain's
/// <c>AddMember</c> behavior, and commits once through the <see cref="IUnitOfWork"/>.
/// </summary>
/// <remarks>
/// WHY TWO ROLE TIERS?
/// Managing membership is an Owner/Maintainer action, so a Viewer/Contributor is Forbidden. But GRANTING
/// the Owner role is strictly more powerful — it hands over the keys — so we reserve that to existing
/// Owners. A Maintainer may add Viewers/Contributors/Maintainers, never Owners.
///
/// WHY VERIFY THE USER EXISTS SEPARATELY?
/// The domain's AddMember only guards against DUPLICATES within the project; it has no way to know
/// whether the userId refers to a real account. Adding a phantom user would create a dangling membership
/// row (a FK to a non-existent user, or an orphan if FKs are lax). We check existence here — the one
/// place with database access — before mutating the aggregate.
/// </remarks>
public sealed class AddMemberCommandHandler : ICommandHandler<AddMemberCommand, AddMemberResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AddMemberCommandHandler> _logger;

    public AddMemberCommandHandler(
        IApplicationDbContext context,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork,
        ILogger<AddMemberCommandHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<AddMemberResponse>> Handle(
        AddMemberCommand request, CancellationToken cancellationToken)
    {
        // 1. Resolve the caller. Adding a member is attributed (UpdatedBy) and authorized => no principal
        //    means 401 before we touch the database.
        if (_currentUser.UserId is not { } callerId)
        {
            _logger.LogWarning("AddMember reached the handler without an authenticated user.");
            return Result.Failure<AddMemberResponse>(Error.Unauthorized(
                "Members.Unauthenticated",
                "You must be signed in to manage project members."));
        }

        // 2. Load the aggregate WITH its members, tracked. Include is required: the role check and the
        //    domain's uniqueness invariant both read the member collection, and the load must be tracked
        //    so EF observes the new member on SaveChanges. The global soft-delete filter hides deleted
        //    projects automatically.
        var project = await _context.Projects
            .Include(p => p.Members)
            .SingleOrDefaultAsync(p => p.Id == request.ProjectId, cancellationToken);

        // 3. Collapse "unknown project" and "caller is not a member" into one NotFound — never disclose a
        //    project's existence to someone who cannot see it.
        var callerMembership = project?.Members.SingleOrDefault(m => m.UserId == callerId);
        if (project is null || callerMembership is null)
        {
            _logger.LogInformation(
                "AddMember: project {ProjectId} not found or not visible to user {UserId}.",
                request.ProjectId, callerId);
            return Result.Failure<AddMemberResponse>(MemberErrors.ProjectNotFound(request.ProjectId));
        }

        // 4. Authorize. Managing members requires Maintainer or Owner. The caller IS a member here, so the
        //    honest signal for an insufficient role is 403 (not the 404 we give non-members).
        if (callerMembership.Role is not (ProjectRole.Maintainer or ProjectRole.Owner))
        {
            _logger.LogInformation(
                "AddMember: user {UserId} with role {Role} may not manage members of project {ProjectId}.",
                callerId, callerMembership.Role, request.ProjectId);
            return Result.Failure<AddMemberResponse>(MemberErrors.Forbidden);
        }

        // 5. Only an Owner may grant the Owner role. A Maintainer adding an Owner is a privilege
        //    escalation, so reject it with the finer-grained OwnerOnly error.
        if (request.Role == ProjectRole.Owner && callerMembership.Role != ProjectRole.Owner)
        {
            _logger.LogInformation(
                "AddMember: maintainer {UserId} attempted to grant Owner in project {ProjectId}.",
                callerId, request.ProjectId);
            return Result.Failure<AddMemberResponse>(MemberErrors.OwnerOnly);
        }

        // 6. Verify the target user is a real account. EXISTS over Users; the global soft-delete filter
        //    excludes deactivated accounts. Prevents a dangling membership to a phantom user.
        var userExists = await _context.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == request.UserId, cancellationToken);

        if (!userExists)
        {
            _logger.LogInformation(
                "AddMember: target user {UserId} not found for project {ProjectId}.",
                request.UserId, request.ProjectId);
            return Result.Failure<AddMemberResponse>(MemberErrors.UserNotFound(request.UserId));
        }

        // 7. Delegate to the domain. AddMember enforces "not archived" and "not already a member", stamps
        //    UpdatedBy/UpdatedAt, and raises ProjectMemberAddedDomainEvent. A DomainException here is an
        //    EXPECTED business collision (duplicate/archived), so we surface it as a modeled 409 rather
        //    than letting it become a 500.
        var utcNow = _dateTimeProvider.UtcNow;

        ProjectMember member;
        try
        {
            member = project.AddMember(request.UserId, request.Role, utcNow, callerId);
        }
        catch (DomainException exception)
        {
            _logger.LogInformation(
                exception, "AddMember rejected by domain invariant for project {ProjectId}.",
                request.ProjectId);
            return Result.Failure<AddMemberResponse>(MemberErrors.Conflict(exception.Message));
        }

        // 8. Commit. The project is tracked and the new member is part of its collection, so one
        //    SaveChanges flushes the insert; the domain event is dispatched in the same transaction by
        //    the interceptor.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "User {TargetUserId} added to project {ProjectId} as {Role} by {CallerId}.",
            request.UserId, request.ProjectId, request.Role, callerId);

        return new AddMemberResponse(member.Id);
    }
}
