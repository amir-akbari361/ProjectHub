using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Application.Common;
using ProjectHub.Domain.Entities;
using ProjectHub.Domain.Enums;

namespace ProjectHub.Application.Features.Tasks.AssignTask;

/// <summary>
/// Handles <see cref="AssignTaskCommand"/>. A WRITE-side handler that authorizes the caller against the
/// task's parent project, verifies the target assignee is itself a member of that project, delegates the
/// mutation to the <c>ProjectTask.Assign</c> domain method (which raises TaskAssignedDomainEvent), and
/// commits once via <see cref="IUnitOfWork"/>.
/// </summary>
/// <remarks>
/// WHY LOAD THE PROJECT SEPARATELY FROM THE TASK?
/// The task aggregate does NOT own the membership collection — that lives on the Project aggregate. To
/// both authorize the caller and validate the assignee we need the project's members, so we load the
/// task (tracked, to mutate it) and the project's membership (as a projection, read-only) as two focused
/// queries rather than a wide join that over-fetches.
/// </remarks>
public sealed class AssignTaskCommandHandler : ICommandHandler<AssignTaskCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AssignTaskCommandHandler> _logger;

    public AssignTaskCommandHandler(
        IApplicationDbContext context,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork,
        ILogger<AssignTaskCommandHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> Handle(AssignTaskCommand request, CancellationToken cancellationToken)
    {
        // 1. Resolve the caller. Assigning is attributed and authorized, so no principal => 401.
        if (_currentUser.UserId is not { } userId)
        {
            _logger.LogWarning("AssignTask reached the handler without an authenticated user.");
            return Result.Failure(Error.Unauthorized(
                "Tasks.Unauthenticated",
                "You must be signed in to assign a task."));
        }

        // 2. Load the task TRACKED — we are going to mutate it, so EF must observe the change.
        var task = await _context.ProjectTasks
            .SingleOrDefaultAsync(t => t.Id == request.TaskId, cancellationToken);

        if (task is null)
        {
            _logger.LogInformation(
                "AssignTask: task {TaskId} not found for user {UserId}.", request.TaskId, userId);
            return Result.Failure(TaskErrors.NotFound(request.TaskId));
        }

        // 3. Load the parent project's membership as a read-only projection. We need it twice: to
        //    authorize the caller and to verify the assignee is a member. One query, no aggregate.
        var members = await _context.Projects
            .AsNoTracking()
            .Where(p => p.Id == task.ProjectId)
            .SelectMany(p => p.Members)
            .Select(m => new { m.UserId, m.Role })
            .ToListAsync(cancellationToken);

        // 4. Authorize the caller. A non-member gets the SAME 404 as an unknown task (no disclosure);
        //    a member without a mutating role (Viewer) gets 403.
        var callerMembership = members.SingleOrDefault(m => m.UserId == userId);
        if (callerMembership is null)
        {
            _logger.LogInformation(
                "AssignTask: user {UserId} is not a member of project {ProjectId}.",
                userId, task.ProjectId);
            return Result.Failure(TaskErrors.NotFound(request.TaskId));
        }

        if (callerMembership.Role < ProjectRole.Contributor)
        {
            _logger.LogInformation(
                "AssignTask: user {UserId} with role {Role} may not assign tasks in project {ProjectId}.",
                userId, callerMembership.Role, task.ProjectId);
            return Result.Failure(TaskErrors.Forbidden);
        }

        // 5. Validate the target assignee is a member of the same project. You cannot assign work to
        //    someone who has no access to the board — that would create an orphaned assignment. Modeled
        //    as a 409 Conflict because the request collides with the project's membership state.
        var assigneeIsMember = members.Any(m => m.UserId == request.AssigneeId);
        if (!assigneeIsMember)
        {
            _logger.LogInformation(
                "AssignTask: assignee {AssigneeId} is not a member of project {ProjectId}.",
                request.AssigneeId, task.ProjectId);
            return Result.Failure(TaskErrors.Conflict(
                "The assignee must be a member of the task's project."));
        }

        // 6. Delegate to the domain. Assign sets the AssigneeId, stamps UpdatedBy/UpdatedAt, and raises
        //    TaskAssignedDomainEvent. The task is already tracked, so no explicit Update is needed.
        task.Assign(request.AssigneeId, _dateTimeProvider.UtcNow, userId);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Task {TaskId} assigned to {AssigneeId} by user {UserId}.",
            request.TaskId, request.AssigneeId, userId);

        return Result.Success();
    }
}
