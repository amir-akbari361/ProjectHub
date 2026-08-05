using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Application.Common;
using ProjectHub.Domain.Entities;

namespace ProjectHub.Application.Features.Projects.ListProjects;

/// <summary>
/// Handles <see cref="ListProjectsQuery"/>. A READ-side handler: it composes a single, tailored SQL
/// statement over <see cref="IApplicationDbContext"/> — filtered to the caller's memberships, narrowed
/// by the optional search/status filters, ordered by the whitelisted sort column, and sliced to one
/// page. It NEVER materializes a <c>Project</c> aggregate; it projects straight into the lean
/// <see cref="ProjectListItemResponse"/> so only the columns the grid needs cross the wire.
/// </summary>
/// <remarks>
/// WHY BUILD THE QUERY AS <c>IQueryable</c> STEP BY STEP?
/// Each <c>Where</c>/<c>OrderBy</c> only appends to an expression tree — nothing executes until the
/// terminal <c>CountAsync</c>/<c>ToListAsync</c>. That lets us conditionally add filters (search, status)
/// and still produce ONE SQL query, instead of filtering in memory after over-fetching.
///
/// WHY TWO ROUND-TRIPS (Count THEN the page)?
/// A paged list needs both the slice AND the total (for page-count math). Those are two different
/// shapes, so they are two queries against the SAME filtered <c>IQueryable</c>. The count runs against
/// the filter without the OrderBy/Skip/Take, keeping it cheap.
/// </remarks>
public sealed class ListProjectsQueryHandler
    : IQueryHandler<ListProjectsQuery, PagedList<ProjectListItemResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<ListProjectsQueryHandler> _logger;

    public ListProjectsQueryHandler(
        IApplicationDbContext context,
        ICurrentUser currentUser,
        ILogger<ListProjectsQueryHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<PagedList<ProjectListItemResponse>>> Handle(
        ListProjectsQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Resolve the caller. Visibility is scoped to membership, so an unauthenticated request has
        //    no set of projects it could legitimately see — fail fast with 401 before touching the DB.
        if (_currentUser.UserId is not { } userId)
        {
            _logger.LogWarning("ListProjects reached the handler without an authenticated user.");
            return Result.Failure<PagedList<ProjectListItemResponse>>(Error.Unauthorized(
                "Projects.Unauthenticated",
                "You must be signed in to list projects."));
        }

        // 2. Start from the caller's memberships only. AsNoTracking() because this is a pure read; the
        //    global soft-delete filter already excludes deleted projects, so we don't repeat it here.
        var query = _context.Projects
            .AsNoTracking()
            .Where(p => p.Members.Any(m => m.UserId == userId));

        // 3. Optional filters. Each is appended to the expression tree only when supplied, so an omitted
        //    filter adds no SQL. The search is a case-insensitive contains against the project name.
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(p => EF.Functions.Like(p.Name.Value, $"%{term}%"));
        }

        if (request.Status is { } status)
        {
            query = query.Where(p => p.Status == status);
        }

        // 4. Total BEFORE paging — the denominator for page-count math. Runs against the filtered set
        //    without OrderBy/Skip/Take, so SQL Server can answer it with a cheap COUNT.
        var totalCount = await query.CountAsync(cancellationToken);

        // 5. Deterministic ordering. We branch on the whitelisted enum (never a client string) and add
        //    Id as a tiebreaker so pages never overlap or drop rows when the sort key has duplicates.
        query = ApplyOrdering(query, request);

        // 6. Slice to the requested page and project into the lean DTO in the SAME query. MemberCount is
        //    a correlated COUNT(*) so we never load membership rows just to count them.
        var items = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(p => new ProjectListItemResponse(
                p.Id,
                p.Name.Value,
                p.Description,
                p.Status,
                p.Members.Count,
                p.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Listed {Count} of {Total} projects for user {UserId} (page {Page}).",
            items.Count, totalCount, userId, request.PageNumber);

        return new PagedList<ProjectListItemResponse>(
            items, totalCount, request.PageNumber, request.PageSize);
    }

    // Kept private and static: it is a pure transformation of the query with no dependency on instance
    // state. Branching on the enum keeps the ORDER BY on a closed, indexed set of columns.
    private static IQueryable<Project> ApplyOrdering(IQueryable<Project> query, ListProjectsQuery request)
    {
        return request.SortBy switch
        {
            ProjectSortBy.Name => request.SortDescending
                ? query.OrderByDescending(p => p.Name.Value).ThenByDescending(p => p.Id)
                : query.OrderBy(p => p.Name.Value).ThenBy(p => p.Id),
            ProjectSortBy.Status => request.SortDescending
                ? query.OrderByDescending(p => p.Status).ThenByDescending(p => p.Id)
                : query.OrderBy(p => p.Status).ThenBy(p => p.Id),
            _ => request.SortDescending
                ? query.OrderByDescending(p => p.CreatedAtUtc).ThenByDescending(p => p.Id)
                : query.OrderBy(p => p.CreatedAtUtc).ThenBy(p => p.Id)
        };
    }
}
