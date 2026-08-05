using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Application.Common;
using ProjectHub.Domain.Enums;

namespace ProjectHub.Application.Features.Tasks.UpdateTaskPriority;

/// <summary>
/// Handles <see cref="UpdateTaskPriorityCommand"/>. A WRITE-side handler that authorizes the caller
/// against the task's parent project and delegates the change to <c>ProjectTask.UpdatePriority</c>.
/// Unlike ChangeStatus there is no domain guard to catch — reassigning the same priority is a harmless
/// idempotent write — so this is the simplest of the task-mutation handlers.
/// </summary>
public sealed class UpdateTaskPriorityCommandHandler : ICommandHandler<UpdateTaskPriorityCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateTaskPriorityCommandHandler> _logger;

    public UpdateTaskPriorityCommandHandler(
        IApplicationDbContext context,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork,
        ILogger<UpdateTaskPriorityCommandHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> Handle(
        UpdateTaskPriorityCommand request, CancellationToken cancellationToken)
    {
        // 1. Resolve the caller. Attributed and authorized, so no principal => 401.
        if (_currentUser.UserId is not { } userId)
        {
            _logger.LogWarning("UpdateTaskPriority reached the handler without an authenticated user.");
            return Result.Failure(Error.Unauthorized(
                "Tasks.Unauthenticated",
                "You must be signed in to change a task's priority."));
        }

        // 2. Load the task TRACKED — we are going to mutate it.
        var task = await _context.ProjectTasks
            .SingleOrDefaultAsync(t => t.Id == request.TaskId, cancellationToken);

        if (task is null)
        {
            _logger.LogInformation(
                "UpdateTaskPriority: task {TaskId} not found for user {UserId}.",
                request.TaskId, userId);
            return Result.Failure(TaskErrors.NotFound(request.TaskId));
        }

        // 3. Authorize the caller against the parent project's membership (read-only projection). A
        //    non-member gets 404 (no disclosure); an under-privileged member (Viewer) gets 403.
        var callerRole = await _context.Projects
            .AsNoTracking()
            .Where(p => p.Id == task.ProjectId)
            .SelectMany(p => p.Members)
            .Where(m => m.UserId == userId)
            .Select(m => (ProjectRole?)m.Role)
            .SingleOrDefaultAsync(cancellationToken);

        if (callerRole is null)
        {
            _logger.LogInformation(
                "UpdateTaskPriority: user {UserId} is not a member of project {ProjectId}.",
                userId, task.ProjectId);
            return Result.Failure(TaskErrors.NotFound(request.TaskId));
        }

        if (callerRole < ProjectRole.Contributor)
        {
            _logger.LogInformation(
                "UpdateTaskPriority: user {UserId} with role {Role} may not change task priority.",
                userId, callerRole);
            return Result.Failure(TaskErrors.Forbidden);
        }

        // 4. Delegate to the domain and commit once.
        task.UpdatePriority(request.Priority, _dateTimeProvider.UtcNow, userId);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Task {TaskId} priority set to {Priority} by user {UserId}.",
            request.TaskId, request.Priority, userId);

        return Result.Success();
    }
}
