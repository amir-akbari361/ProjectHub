using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Application.Common;
using ProjectHub.Application.Features.Projects;
using ProjectHub.Domain.Entities;
using ProjectHub.Domain.Enums;
using ProjectHub.Domain.Exceptions;
using ProjectHub.Domain.ValueObjects;

namespace ProjectHub.Application.Features.Sprints.CreateSprint;

/// <summary>
/// Handles <see cref="CreateSprintCommand"/>. A thin orchestrator: authorize the caller against the parent
/// project's membership, build the <see cref="Sprint"/> aggregate through its domain factory (which
/// enforces invariants and starts the sprint Planned), and commit once via <see cref="IUnitOfWork"/>.
/// </summary>
/// <remarks>
/// WHY LOAD THE PROJECT (not just trust the ProjectId)?
/// Creating a sprint is a mutation scoped to a project the caller must belong to with a mutating role.
/// A Viewer — or a non-member who guessed a project id — must not be able to create sprints. We load the
/// project WITH its members to make that decision, and to enforce the "archived projects are read-only"
/// rule before we persist anything. This mirrors <c>CreateTaskCommandHandler</c> exactly.
///
/// WHY COERCE THE DATES TO UTC?
/// <c>DateRange.Create</c> rejects any boundary whose <see cref="DateTimeKind"/> is not UTC. The dates
/// arrive off the wire as <see cref="DateTimeKind.Unspecified"/> (JSON carries no kind), so we stamp them
/// UTC before constructing the range. The construction is wrapped so a <see cref="DomainException"/> from
/// a bad range becomes a modeled 400 rather than bubbling to a 500.
/// </remarks>
public sealed class CreateSprintCommandHandler
    : ICommandHandler<CreateSprintCommand, CreateSprintResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IRepository<Sprint> _sprintRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateSprintCommandHandler> _logger;

    public CreateSprintCommandHandler(
        IApplicationDbContext context,
        IRepository<Sprint> sprintRepository,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork,
        ILogger<CreateSprintCommandHandler> logger)
    {
        _context = context;
        _sprintRepository = sprintRepository;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<CreateSprintResponse>> Handle(
        CreateSprintCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Resolve the caller. Sprint creation is attributed and authorized, so no principal means the
        //    endpoint was reached without authentication — fail fast with a 401.
        if (_currentUser.UserId is not { } userId)
        {
            _logger.LogWarning("CreateSprint reached the handler without an authenticated user.");
            return Result.Failure<CreateSprintResponse>(Error.Unauthorized(
                "Sprints.Unauthenticated",
                "You must be signed in to create a sprint."));
        }

        // 2. Load the parent project WITH members. We need the membership rows in memory to authorize, and
        //    the project's status to enforce the archived-is-read-only rule. The global soft-delete filter
        //    hides deleted projects, so a missing row is a genuine 404.
        var project = await _context.Projects
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId, cancellationToken);

        if (project is null)
        {
            _logger.LogInformation(
                "CreateSprint: project {ProjectId} not found for user {UserId}.",
                request.ProjectId, userId);
            return Result.Failure<CreateSprintResponse>(SprintErrors.ProjectNotFound(request.ProjectId));
        }

        // 3. Authorize. The caller must be a member; a Viewer may see sprints but not create them, so we
        //    require at least Contributor. A non-member gets the SAME 404 as an unknown id (we do not
        //    reveal the project's existence to outsiders); an under-privileged member gets 403.
        var membership = project.Members.FirstOrDefault(m => m.UserId == userId);
        if (membership is null)
        {
            _logger.LogInformation(
                "CreateSprint: user {UserId} is not a member of project {ProjectId}.",
                userId, request.ProjectId);
            return Result.Failure<CreateSprintResponse>(SprintErrors.ProjectNotFound(request.ProjectId));
        }

        if (membership.Role < ProjectRole.Contributor)
        {
            _logger.LogInformation(
                "CreateSprint: user {UserId} with role {Role} may not create sprints in project {ProjectId}.",
                userId, membership.Role, request.ProjectId);
            return Result.Failure<CreateSprintResponse>(SprintErrors.Forbidden);
        }

        // 4. Enforce the archived-is-read-only rule at the application boundary so the client gets a clean
        //    409 rather than a 500 from a domain guard.
        if (project.Status == ProjectStatus.Archived)
        {
            return Result.Failure<CreateSprintResponse>(ProjectErrors.Archived);
        }

        // 5. Build the schedule value object. The wire carries kind-less DateTimes; DateRange.Create demands
        //    UTC, so stamp both boundaries UTC first. A bad range (end <= start) throws DomainException,
        //    which we translate into a modeled 400 instead of letting it become a 500. The validator already
        //    rejects that shape, so this is belt-and-suspenders for a request that slipped past it.
        DateRange schedule;
        try
        {
            schedule = DateRange.Create(
                DateTime.SpecifyKind(request.StartUtc, DateTimeKind.Utc),
                DateTime.SpecifyKind(request.EndUtc, DateTimeKind.Utc));
        }
        catch (DomainException exception)
        {
            _logger.LogInformation(
                exception, "CreateSprint: invalid schedule for project {ProjectId}.", request.ProjectId);
            return Result.Failure<CreateSprintResponse>(SprintErrors.InvalidSchedule(exception.Message));
        }

        // 6. Build the aggregate through its factory. Sprint.Create sets status Planned and stamps audit
        //    fields. The handler supplies only the clock reading and the creator id.
        var utcNow = _dateTimeProvider.UtcNow;
        var sprint = Sprint.Create(request.ProjectId, request.Name, schedule, utcNow, userId);

        // 7. Stage the insert and commit once.
        await _sprintRepository.AddAsync(sprint, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Sprint {SprintId} created in project {ProjectId} by user {UserId}.",
            sprint.Id, request.ProjectId, userId);

        return new CreateSprintResponse(sprint.Id, sprint.Name);
    }
}
