using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Application.Common;

namespace ProjectHub.Application.Features.Comments.ListComments;

/// <summary>
/// Handles <see cref="ListCommentsQuery"/>. A READ-side handler that verifies the caller can see the
/// parent task's project, then composes a single paged SQL statement over the task's comment thread,
/// projecting straight into <see cref="CommentResponse"/>. It NEVER materializes a <c>Comment</c>
/// aggregate — the read side stays free of domain invariants and change tracking.
/// </summary>
/// <remarks>
/// WHY OLDEST-FIRST?
/// A comment thread reads like a conversation, so we order ascending by creation time (with Id as a
/// stable tiebreaker) — the natural reading order and the one that keeps pagination deterministic.
/// </remarks>
public sealed class ListCommentsQueryHandler
    : IQueryHandler<ListCommentsQuery, PagedList<CommentResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<ListCommentsQueryHandler> _logger;

    public ListCommentsQueryHandler(
        IApplicationDbContext context,
        ICurrentUser currentUser,
        ILogger<ListCommentsQueryHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<PagedList<CommentResponse>>> Handle(
        ListCommentsQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Resolve the caller. Comment threads are scoped to project membership, so an unauthenticated
        //    request has nothing it could legitimately see — fail fast with 401 before touching the DB.
        if (_currentUser.UserId is not { } userId)
        {
            _logger.LogWarning("ListComments reached the handler without an authenticated user.");
            return Result.Failure<PagedList<CommentResponse>>(Error.Unauthorized(
                "Comments.Unauthenticated",
                "You must be signed in to view comments."));
        }

        // 2. Verify the caller can see the parent task's project. A cheap EXISTS over task -> project ->
        //    members. Unknown task or non-member both collapse into the SAME NotFound (no disclosure).
        var taskVisible = await _context.ProjectTasks
            .AsNoTracking()
            .Where(t => t.Id == request.TaskId)
            .AnyAsync(
                t => _context.Projects.Any(
                    p => p.Id == t.ProjectId && p.Members.Any(m => m.UserId == userId)),
                cancellationToken);

        if (!taskVisible)
        {
            _logger.LogInformation(
                "ListComments: task {TaskId} not found or not visible to user {UserId}.",
                request.TaskId, userId);
            return Result.Failure<PagedList<CommentResponse>>(
                CommentErrors.TaskNotFound(request.TaskId));
        }

        // 3. Base query: this task's comments. AsNoTracking() — pure read; the global soft-delete filter
        //    already excludes deleted comments.
        var query = _context.Comments
            .AsNoTracking()
            .Where(c => c.TaskId == request.TaskId);

        // 4. Total BEFORE paging — the denominator for page-count math.
        var totalCount = await query.CountAsync(cancellationToken);

        // 5. Order oldest-first (Id tiebreaker for stable pages), slice to the page, and project into the
        //    lean DTO in the SAME query so EF emits one round-trip.
        var items = await query
            .OrderBy(c => c.CreatedAtUtc)
            .ThenBy(c => c.Id)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => new CommentResponse(
                c.Id,
                c.TaskId,
                c.AuthorId,
                c.Body.Value,
                c.IsEdited,
                c.CreatedAtUtc,
                c.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Listed {Count} of {Total} comments for task {TaskId} (page {Page}).",
            items.Count, totalCount, request.TaskId, request.PageNumber);

        return new PagedList<CommentResponse>(
            items, totalCount, request.PageNumber, request.PageSize);
    }
}
