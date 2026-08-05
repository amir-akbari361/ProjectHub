using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Application.Common;
using ProjectHub.Domain.Enums;
using ProjectHub.Domain.Exceptions;

namespace ProjectHub.Application.Features.Projects.ArchiveProject;

/// <summary>
/// Handles <see cref="ArchiveProjectCommand"/>. Loads the aggregate with its members, authorizes the
/// caller as an Owner, then delegates the state transition to <c>Project.Archive</c> which enforces the
/// "already archived" invariant and raises <c>ProjectArchivedDomainEvent</c>. Commits once via
/// <see cref="IUnitOfWork"/>.
/// </summary>
/// <remarks>
/// WHY IS ARCHIVING OWNER-ONLY WHILE EDITING WAS MAINTAINER-OR-OWNER?
/// Retiring a project is higher-impact and effectively irreversible from a user's day-to-day view (the
/// whole board goes read-only), so it sits at the top of the authorization ladder. Maintainers run the
/// project; only Owners decide it is done. Encoding that difference per-command — rather than a single
/// blanket "can edit" check — is exactly why we model each use case as its own command.
///
/// WHY CATCH <c>DomainException</c> HERE TOO?
/// Archiving an already-archived project is an expected conflict, not a fault. We translate it into
/// <see cref="ProjectErrors.Archived"/> (409) so the exception pipeline stays reserved for genuine bugs.
/// </remarks>
public sealed class ArchiveProjectCommandHandler : ICommandHandler<ArchiveProjectCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ArchiveProjectCommandHandler> _logger;

    public ArchiveProjectCommandHandler(
        IApplicationDbContext context,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork,
        ILogger<ArchiveProjectCommandHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> Handle(ArchiveProjectCommand request, CancellationToken cancellationToken)
    {
        // 1. Require an authenticated caller — archiving must be attributed and authorized.
        if (_currentUser.UserId is not { } userId)
        {
            _logger.LogWarning("ArchiveProject reached the handler without an authenticated user.");
            return Result.Failure(Error.Unauthorized(
                "Projects.Unauthenticated",
                "You must be signed in to archive a project."));
        }

        // 2. Load the aggregate with members, tracked (this is a write). The soft-delete filter hides
        //    deleted projects automatically.
        var project = await _context.Projects
            .Include(p => p.Members)
            .SingleOrDefaultAsync(p => p.Id == request.ProjectId, cancellationToken);

        // 3. Non-members get NotFound (existence stays hidden); the check also covers unknown ids.
        var membership = project?.Members.SingleOrDefault(m => m.UserId == userId);
        if (project is null || membership is null)
        {
            _logger.LogInformation(
                "Project {ProjectId} not found or not visible to user {UserId} on archive.",
                request.ProjectId, userId);
            return Result.Failure(ProjectErrors.NotFound(request.ProjectId));
        }

        // 4. Owner-only. A member who is not an Owner already knows the project exists, so 403 is the
        //    honest signal rather than a misleading 404.
        if (membership.Role is not ProjectRole.Owner)
        {
            _logger.LogInformation(
                "User {UserId} with role {Role} attempted to archive project {ProjectId}.",
                userId, membership.Role, request.ProjectId);
            return Result.Failure(ProjectErrors.Forbidden);
        }

        // 5. Delegate the transition to the domain. Archive guards against double-archiving and raises
        //    the domain event; we translate its guard into a modeled Conflict.
        try
        {
            project.Archive(_dateTimeProvider.UtcNow, userId);
        }
        catch (DomainException exception)
        {
            _logger.LogInformation(
                exception, "Archive rejected by domain invariant for project {ProjectId}.",
                request.ProjectId);
            return Result.Failure(ProjectErrors.Archived);
        }

        // 6. One commit flushes the status change and dispatches the domain event via the interceptor.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Project {ProjectId} archived by user {UserId}.", request.ProjectId, userId);

        return Result.Success();
    }
}
