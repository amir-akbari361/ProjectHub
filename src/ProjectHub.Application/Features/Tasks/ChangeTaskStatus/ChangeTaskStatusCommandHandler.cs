using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Application.Common;
using ProjectHub.Domain.Enums;
using ProjectHub.Domain.Exceptions;

namespace ProjectHub.Application.Features.Tasks.ChangeTaskStatus;

/// <summary>
/// Handles <see cref="ChangeTaskStatusCommand"/>. A WRITE-side handler that authorizes the caller
/// against the task's parent project, delegates the transition to <c>ProjectTask.ChangeStatus</c>
/// (which raises TaskStatusChangedDomainEvent and guards against no-op transitions), and commits once.
/// </summary>
/// <remarks>
/// WHY CATCH <c>DomainException</c> HERE?
/// "The task is already in that status" is an EXPECTED business outcome, not a bug. We translate the
/// domain guard into a modeled 409 Conflict so the client gets a clean, typed error rather than a 500,
/// keeping the exception channel reserved for the truly unexpected.
/// </remarks>
public sealed class ChangeTaskStatusCommandHandler : ICommandHandler<ChangeTaskStatusCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ChangeTaskStatusCommandHandler> _logger;

    public ChangeTaskStatusCommandHandler(
        IApplicationDbContext context,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork,
        ILogger<ChangeTaskStatusCommandHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> Handle(
        ChangeTaskStatusCommand request, CancellationToken cancellationToken)
    {
        // 1. Resolve the caller. Transitioning is attributed and authorized, so no principal => 401.
        if (_currentUser.UserId is not { } userId)
        {
            _logger.LogWarning("ChangeTaskStatus reached the handler without an authenticated user.");
            return Result.Failure(Error.Unauthorized(
                "Tasks.Unauthenticated",
                "You must be signed in to change a task's status."));
        }

        // 2. Load the task TRACKED — we are going to mutate it.
        var task = await _context.ProjectTasks
            .SingleOrDefaultAsync(t => t.Id == request.TaskId, cancellationToken);

        if (task is null)
        {
            _logger.LogInformation(
                "ChangeTaskStatus: task {TaskId} not found for user {UserId}.", request.TaskId, userId);
            return Result.Failure(TaskErrors.NotFound(request.TaskId));
        }

        // 3. Authorize the caller against the parent project's membership. Read-only projection: we only
        //    need the caller's role, not the whole aggregate. A non-member gets 404 (no disclosure); an
        //    under-privileged member (Viewer) gets 403.
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
                "ChangeTaskStatus: user {UserId} is not a member of project {ProjectId}.",
                userId, task.ProjectId);
            return Result.Failure(TaskErrors.NotFound(request.TaskId));
        }

        if (callerRole < ProjectRole.Contributor)
        {
            _logger.LogInformation(
                "ChangeTaskStatus: user {UserId} with role {Role} may not transition tasks.",
                userId, callerRole);
            return Result.Failure(TaskErrors.Forbidden);
        }

        // 4. Delegate to the domain. ChangeStatus throws if the task is already in the target status;
        //    we translate that guard into a modeled 409 instead of letting it become a 500.
        try
        {
            task.ChangeStatus(request.NewStatus, _dateTimeProvider.UtcNow, userId);
        }
        catch (DomainException exception)
        {
            _logger.LogInformation(
                exception, "ChangeTaskStatus rejected by domain invariant for task {TaskId}.",
                request.TaskId);
            return Result.Failure(TaskErrors.Conflict(exception.Message));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Task {TaskId} moved to status {Status} by user {UserId}.",
            request.TaskId, request.NewStatus, userId);

        return Result.Success();
    }
}
