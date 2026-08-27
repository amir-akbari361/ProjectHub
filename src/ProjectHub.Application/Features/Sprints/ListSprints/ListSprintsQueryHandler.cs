using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Application.Common;

namespace ProjectHub.Application.Features.Sprints.ListSprints;

/// <summary>
/// Handles <see cref="ListSprintsQuery"/>. A READ-side handler that composes one tailored SQL statement
/// over <see cref="IApplicationDbContext"/>: scoped to a project the caller can see, ordered by schedule
/// start, and projected straight into the lean <see cref="SprintResponse"/>. It NEVER materializes a
/// <c>Sprint</c> aggregate. Mirrors <c>ListTasksQueryHandler</c> minus the filtering/paging, since sprints
/// per project are few.
/// </summary>
public sealed class ListSprintsQueryHandler
    : IQueryHandler<ListSprintsQuery, IReadOnlyList<SprintResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<ListSprintsQueryHandler> _logger;

    public ListSprintsQueryHandler(
        IApplicationDbContext context,
        ICurrentUser currentUser,
        ILogger<ListSprintsQueryHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<SprintResponse>>> Handle(
        ListSprintsQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Resolve the caller. Visibility is scoped to membership, so an unauthenticated request has no
        //    sprints it could legitimately see — fail fast with 401 before touching the DB.
        if (_currentUser.UserId is not { } userId)
        {
            _logger.LogWarning("ListSprints reached the handler without an authenticated user.");
            return Result.Failure<IReadOnlyList<SprintResponse>>(Error.Unauthorized(
                "Sprints.Unauthenticated",
                "You must be signed in to list sprints."));
        }

        // 2. Verify the caller can see the parent project. If not (unknown id or not a member) return the
        //    SAME NotFound as an unknown project — no information disclosure. This is a cheap EXISTS.
        var projectVisible = await _context.Projects
            .AsNoTracking()
            .AnyAsync(
                p => p.Id == request.ProjectId && p.Members.Any(m => m.UserId == userId),
                cancellationToken);

        if (!projectVisible)
        {
            _logger.LogInformation(
                "ListSprints: project {ProjectId} not found or not visible to user {UserId}.",
                request.ProjectId, userId);
            return Result.Failure<IReadOnlyList<SprintResponse>>(
                SprintErrors.ProjectNotFound(request.ProjectId));
        }

        // 3. Project the project's sprints straight into the DTO. AsNoTracking() because this is a pure
        //    read; the global soft-delete filter already excludes deleted sprints. Order by the owned
        //    schedule's start (translates to the schedule_start column), Id as a stable tiebreaker.
        IReadOnlyList<SprintResponse> sprints = await _context.Sprints
            .AsNoTracking()
            .Where(s => s.ProjectId == request.ProjectId)
            .OrderBy(s => s.Schedule.Start)
            .ThenBy(s => s.Id)
            .Select(s => new SprintResponse(
                s.Id,
                s.ProjectId,
                s.Name,
                s.Schedule.Start,
                s.Schedule.End,
                s.Status))
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Listed {Count} sprints for project {ProjectId}.", sprints.Count, request.ProjectId);

        return Result.Success(sprints);
    }
}
