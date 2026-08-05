using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Application.Common;

namespace ProjectHub.Application.Features.Tasks.GetTaskById;

/// <summary>
/// Handles <see cref="GetTaskByIdQuery"/>. A READ-side handler: it depends on
/// <see cref="IApplicationDbContext"/> for LINQ composition rather than the write-side repository,
/// never calls SaveChanges, and projects straight into a <see cref="TaskResponse"/> DTO in a single
/// SQL round-trip. Visibility is enforced by requiring the caller to be a member of the task's parent
/// project — a non-member gets the SAME 404 as an unknown id to avoid leaking task existence.
/// </summary>
public sealed class GetTaskByIdQueryHandler
    : IQueryHandler<GetTaskByIdQuery, TaskResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<GetTaskByIdQueryHandler> _logger;

    public GetTaskByIdQueryHandler(
        IApplicationDbContext context,
        ICurrentUser currentUser,
        ILogger<GetTaskByIdQueryHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<TaskResponse>> Handle(
        GetTaskByIdQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Resolve the caller. Task visibility is scoped to project membership, so an unauthenticated
        //    request can never legitimately return data — fail fast with a 401.
        if (_currentUser.UserId is not { } userId)
        {
            _logger.LogWarning("GetTaskById reached the handler without an authenticated user.");
            return Result.Failure<TaskResponse>(Error.Unauthorized(
                "Tasks.Unauthenticated",
                "You must be signed in to view a task."));
        }

        // 2. Compose the read query. AsNoTracking() skips change-tracker snapshotting for a pure read.
        //    The WHERE clause joins the task to its project's membership so the row is only returned when
        //    the caller belongs to the parent project. We project directly into the DTO so EF emits a
        //    single SELECT that materializes only the columns we need.
        var task = await _context.ProjectTasks
            .AsNoTracking()
            .Where(t => t.Id == request.TaskId
                && _context.Projects.Any(p => p.Id == t.ProjectId
                    && p.Members.Any(m => m.UserId == userId)))
            .Select(t => new TaskResponse(
                t.Id,
                t.ProjectId,
                t.Title.Value,
                t.Description,
                t.Status,
                t.Priority,
                t.AssigneeId,
                t.CreatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);

        // 3. Collapse "doesn't exist" and "not visible" into one NotFound to avoid disclosing the
        //    existence of tasks in projects the caller cannot see.
        if (task is null)
        {
            _logger.LogInformation(
                "Task {TaskId} not found or not visible to user {UserId}.",
                request.TaskId, userId);
            return Result.Failure<TaskResponse>(TaskErrors.NotFound(request.TaskId));
        }

        return task;
    }
}
