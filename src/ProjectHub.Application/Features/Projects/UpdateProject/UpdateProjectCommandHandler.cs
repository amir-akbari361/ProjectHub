using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Application.Common;
using ProjectHub.Domain.Enums;
using ProjectHub.Domain.Exceptions;
using ProjectHub.Domain.ValueObjects;

namespace ProjectHub.Application.Features.Projects.UpdateProject;

/// <summary>
/// Handles <see cref="UpdateProjectCommand"/>. A WRITE-side handler that loads the <c>Project</c>
/// aggregate WITH its members (so it can both authorize the caller and let the domain enforce its
/// archived-project invariants), invokes the domain's <c>Rename</c>/<c>ChangeDescription</c> behaviors,
/// and commits once through <see cref="IUnitOfWork"/>.
/// </summary>
/// <remarks>
/// WHY LOAD THROUGH <see cref="IApplicationDbContext"/> AND NOT <c>IRepository.GetByIdAsync</c>?
/// The generic repository loads by key via <c>FindAsync</c>, which does NOT populate navigation
/// collections — the members would be empty and the role check would wrongly deny everyone. We need the
/// members eagerly, so we compose an <c>Include</c> here. The load is TRACKED (no AsNoTracking) because
/// this is a write: EF must observe the mutations so <c>SaveChangesAsync</c> can persist them. All three
/// abstractions share the same scoped DbContext, so the entity we track here is the one the unit of work
/// commits.
///
/// WHY MAP THE DOMAIN'S <c>DomainException</c> TO A RESULT INSTEAD OF LETTING IT THROW?
/// "The project is archived" is an EXPECTED business outcome, not a bug. Catching it and returning a
/// modeled <see cref="ProjectErrors.Archived"/> (409) keeps the exception channel reserved for the truly
/// unexpected and gives the client a clean, typed error instead of a 500.
/// </remarks>
public sealed class UpdateProjectCommandHandler : ICommandHandler<UpdateProjectCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateProjectCommandHandler> _logger;

    public UpdateProjectCommandHandler(
        IApplicationDbContext context,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork,
        ILogger<UpdateProjectCommandHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> Handle(UpdateProjectCommand request, CancellationToken cancellationToken)
    {
        // 1. Resolve the caller. Editing must be attributed (UpdatedBy) and authorized, so an
        //    unauthenticated request cannot proceed — fail fast with 401.
        if (_currentUser.UserId is not { } userId)
        {
            _logger.LogWarning("UpdateProject reached the handler without an authenticated user.");
            return Result.Failure(Error.Unauthorized(
                "Projects.Unauthenticated",
                "You must be signed in to update a project."));
        }

        // 2. Load the aggregate WITH its members, tracked. Include is required because the role check
        //    and the domain invariants both read the member collection. The global soft-delete filter
        //    already hides deleted projects.
        var project = await _context.Projects
            .Include(p => p.Members)
            .SingleOrDefaultAsync(p => p.Id == request.ProjectId, cancellationToken);

        // 3. Collapse "unknown id" and "caller is not a member" into one NotFound, exactly like the read
        //    side: we never reveal the existence of a project to someone who cannot see it.
        var membership = project?.Members.SingleOrDefault(m => m.UserId == userId);
        if (project is null || membership is null)
        {
            _logger.LogInformation(
                "Project {ProjectId} not found or not visible to user {UserId} on update.",
                request.ProjectId, userId);
            return Result.Failure(ProjectErrors.NotFound(request.ProjectId));
        }

        // 4. Authorize by role. Editing descriptive fields is a Maintainer-or-Owner action; Viewers and
        //    Contributors can see the project but not rename it. The caller IS a member here, so the
        //    honest signal for "insufficient role" is 403 Forbidden (not the 404 we give non-members).
        if (membership.Role is not (ProjectRole.Maintainer or ProjectRole.Owner))
        {
            _logger.LogInformation(
                "User {UserId} with role {Role} attempted to update project {ProjectId}.",
                userId, membership.Role, request.ProjectId);
            return Result.Failure(ProjectErrors.Forbidden);
        }

        // 5. Delegate to the domain. Rename/ChangeDescription enforce the "archived projects are
        //    read-only" invariant and stamp UpdatedBy/UpdatedAt. We translate the domain's guard
        //    (a DomainException) into a modeled Conflict rather than letting it surface as a 500.
        var name = ProjectName.Create(request.Name);
        var utcNow = _dateTimeProvider.UtcNow;

        try
        {
            project.Rename(name, utcNow, userId);
            project.ChangeDescription(request.Description, utcNow, userId);
        }
        catch (DomainException exception)
        {
            _logger.LogInformation(
                exception, "Update rejected by domain invariant for project {ProjectId}.",
                request.ProjectId);
            return Result.Failure(ProjectErrors.Archived);
        }

        // 6. Commit. The project is already tracked, so no explicit Update call is needed — EF detected
        //    the property changes during the domain calls. One SaveChanges flushes them atomically.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Project {ProjectId} updated by user {UserId}.", request.ProjectId, userId);

        return Result.Success();
    }
}
