using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Application.Common;
using ProjectHub.Application.Features.Tasks.GetTaskById;
using ProjectHub.Domain.Entities;

namespace ProjectHub.Application.Features.Tasks.ListTasks;

/// <summary>
/// Handles <see cref="ListTasksQuery"/>. A READ-side handler that composes a single, tailored SQL
/// statement over <see cref="IApplicationDbContext"/> — scoped to a project the caller can see,
/// narrowed by the optional filters, ordered by the whitelisted sort column, and sliced to one page.
/// It NEVER materializes a <c>ProjectTask</c> aggregate; it projects straight into the lean
/// <see cref="TaskResponse"/>.
/// </summary>
public sealed class ListTasksQueryHandler
    : IQueryHandler<ListTasksQuery, PagedList<TaskResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<ListTasksQueryHandler> _logger;

    public ListTasksQueryHandler(
        IApplicationDbContext context,
        ICurrentUser currentUser,
        ILogger<ListTasksQueryHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<PagedList<TaskResponse>>> Handle(
        ListTasksQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Resolve the caller. Visibility is scoped to membership, so an unauthenticated request has
        //    no tasks it could legitimately see — fail fast with 401 before touching the DB.
        if (_currentUser.UserId is not { } userId)
        {
            _logger.LogWarning("ListTasks reached the handler without an authenticated user.");
            return Result.Failure<PagedList<TaskResponse>>(Error.Unauthorized(
                "Tasks.Unauthenticated",
                "You must be signed in to list tasks."));
        }

        // 2. Verify the caller can see the parent project. If not (unknown id or not a member) we return
        //    the SAME NotFound as an unknown project — no information disclosure. This is a cheap EXISTS.
        var projectVisible = await _context.Projects
            .AsNoTracking()
            .AnyAsync(
                p => p.Id == request.ProjectId && p.Members.Any(m => m.UserId == userId),
                cancellationToken);

        if (!projectVisible)
        {
            _logger.LogInformation(
                "ListTasks: project {ProjectId} not found or not visible to user {UserId}.",
                request.ProjectId, userId);
            return Result.Failure<PagedList<TaskResponse>>(
                TaskErrors.ProjectNotFound(request.ProjectId));
        }

        // 3. Start from the project's tasks. AsNoTracking() because this is a pure read; the global
        //    soft-delete filter already excludes deleted tasks.
        var query = _context.ProjectTasks
            .AsNoTracking()
            .Where(t => t.ProjectId == request.ProjectId);

        // 4. Optional filters. Each is appended only when supplied, so an omitted filter adds no SQL.
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(t => EF.Functions.Like(t.Title.Value, $"%{term}%"));
        }

        if (request.Status is { } status)
        {
            query = query.Where(t => t.Status == status);
        }

        if (request.Priority is { } priority)
        {
            query = query.Where(t => t.Priority == priority);
        }

        if (request.AssigneeId is { } assigneeId)
        {
            query = query.Where(t => t.AssigneeId == assigneeId);
        }

        // 5. Total BEFORE paging — the denominator for page-count math. Cheap COUNT against the filter.
        var totalCount = await query.CountAsync(cancellationToken);

        // 6. Deterministic ordering. Branch on the whitelisted enum (never a client string) and add Id
        //    as a tiebreaker so pages never overlap or drop rows when the sort key has duplicates.
        query = ApplyOrdering(query, request);

        // 7. Slice to the requested page and project into the lean DTO in the SAME query.
        var items = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(t => new TaskResponse(
                t.Id,
                t.ProjectId,
                t.Title.Value,
                t.Description,
                t.Status,
                t.Priority,
                t.AssigneeId,
                t.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Listed {Count} of {Total} tasks for project {ProjectId} (page {Page}).",
            items.Count, totalCount, request.ProjectId, request.PageNumber);

        return new PagedList<TaskResponse>(
            items, totalCount, request.PageNumber, request.PageSize);
    }

    // Pure transformation of the query with no dependency on instance state; branching on the enum
    // keeps the ORDER BY on a closed, indexed set of columns.
    private static IQueryable<ProjectTask> ApplyOrdering(
        IQueryable<ProjectTask> query, ListTasksQuery request)
    {
        return request.SortBy switch
        {
            TaskSortBy.Title => request.SortDescending
                ? query.OrderByDescending(t => t.Title.Value).ThenByDescending(t => t.Id)
                : query.OrderBy(t => t.Title.Value).ThenBy(t => t.Id),
            TaskSortBy.Status => request.SortDescending
                ? query.OrderByDescending(t => t.Status).ThenByDescending(t => t.Id)
                : query.OrderBy(t => t.Status).ThenBy(t => t.Id),
            TaskSortBy.Priority => request.SortDescending
                ? query.OrderByDescending(t => t.Priority).ThenByDescending(t => t.Id)
                : query.OrderBy(t => t.Priority).ThenBy(t => t.Id),
            _ => request.SortDescending
                ? query.OrderByDescending(t => t.CreatedAtUtc).ThenByDescending(t => t.Id)
                : query.OrderBy(t => t.CreatedAtUtc).ThenBy(t => t.Id)
        };
    }
}
