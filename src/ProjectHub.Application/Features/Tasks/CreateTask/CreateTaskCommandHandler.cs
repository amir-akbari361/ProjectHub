using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Application.Common;
using ProjectHub.Domain.Entities;
using ProjectHub.Domain.Enums;
using ProjectHub.Domain.ValueObjects;

namespace ProjectHub.Application.Features.Tasks.CreateTask;

/// <summary>
/// Handles <see cref="CreateTaskCommand"/>. A thin orchestrator: authorize the caller against the
/// parent project's membership, build the <see cref="ProjectTask"/> aggregate through its domain
/// factory (which enforces invariants and raises TaskCreatedDomainEvent), and commit once via
/// <see cref="IUnitOfWork"/>.
/// </summary>
/// <remarks>
/// WHY LOAD THE PROJECT (not just trust the ProjectId)?
/// Creating a task is a mutation scoped to a project the caller must belong to with a mutating role.
/// A Viewer — or a non-member who guessed a project id — must not be able to create tasks. We load the
/// project WITH its members to make that decision, and to enforce the "archived projects are read-only"
/// rule before we persist anything.
/// </remarks>
public sealed class CreateTaskCommandHandler
    : ICommandHandler<CreateTaskCommand, CreateTaskResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IRepository<ProjectTask> _taskRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateTaskCommandHandler> _logger;

    public CreateTaskCommandHandler(
        IApplicationDbContext context,
        IRepository<ProjectTask> taskRepository,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork,
        ILogger<CreateTaskCommandHandler> logger)
    {
        _context = context;
        _taskRepository = taskRepository;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<CreateTaskResponse>> Handle(
        CreateTaskCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Resolve the caller. Task creation is attributed and authorized, so no principal means the
        //    endpoint was reached without authentication — fail fast with a 401.
        if (_currentUser.UserId is not { } userId)
        {
            _logger.LogWarning("CreateTask reached the handler without an authenticated user.");
            return Result.Failure<CreateTaskResponse>(Error.Unauthorized(
                "Tasks.Unauthenticated",
                "You must be signed in to create a task."));
        }

        // 2. Load the parent project WITH members. Tracking is irrelevant here (we don't mutate the
        //    project), but we need the membership rows in memory to authorize, and the project's status
        //    to enforce the archived-is-read-only rule. The global soft-delete filter hides deleted
        //    projects, so a missing row is a genuine 404.
        var project = await _context.Projects
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId, cancellationToken);

        if (project is null)
        {
            _logger.LogInformation(
                "CreateTask: project {ProjectId} not found for user {UserId}.",
                request.ProjectId, userId);
            return Result.Failure<CreateTaskResponse>(TaskErrors.ProjectNotFound(request.ProjectId));
        }

        // 3. Authorize. The caller must be a member; a Viewer may see the board but not create tasks, so
        //    we require at least Contributor. A non-member gets the SAME 404 as an unknown id (we do not
        //    reveal the project's existence to outsiders); an under-privileged member gets 403.
        var membership = project.Members.FirstOrDefault(m => m.UserId == userId);
        if (membership is null)
        {
            _logger.LogInformation(
                "CreateTask: user {UserId} is not a member of project {ProjectId}.",
                userId, request.ProjectId);
            return Result.Failure<CreateTaskResponse>(TaskErrors.ProjectNotFound(request.ProjectId));
        }

        if (membership.Role < ProjectRole.Contributor)
        {
            _logger.LogInformation(
                "CreateTask: user {UserId} with role {Role} may not create tasks in project {ProjectId}.",
                userId, membership.Role, request.ProjectId);
            return Result.Failure<CreateTaskResponse>(TaskErrors.Forbidden);
        }

        // 4. Enforce the archived-is-read-only rule at the application boundary so the client gets a
        //    clean 409 rather than a 500 from a domain guard.
        if (project.Status == ProjectStatus.Archived)
        {
            return Result.Failure<CreateTaskResponse>(Projects.ProjectErrors.Archived);
        }

        // 5. Parse the title into its value object. TaskTitle.Create trims and length-checks; the
        //    ValidationBehavior already rejected bad shapes, so a throw here would indicate a validation
        //    gap (correctly surfaced as a 500).
        var title = TaskTitle.Create(request.Title);

        // 6. Build the aggregate through its factory. ProjectTask.Create sets status Todo, stamps audit
        //    fields, and raises TaskCreatedDomainEvent. The handler supplies only the clock reading and
        //    the creator id.
        var utcNow = _dateTimeProvider.UtcNow;
        var task = ProjectTask.Create(
            request.ProjectId,
            title,
            request.Description,
            request.Priority,
            utcNow,
            userId);

        // 7. Stage the insert and commit once. SaveChangesAsync flushes the row and dispatches the
        //    domain event through PublishDomainEventsInterceptor.
        await _taskRepository.AddAsync(task, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Task {TaskId} created in project {ProjectId} by user {UserId}.",
            task.Id, request.ProjectId, userId);

        return new CreateTaskResponse(task.Id, task.Title.Value);
    }
}
