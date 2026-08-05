using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Application.Common;

namespace ProjectHub.Application.Features.Search.GlobalSearch;

/// <summary>
/// Handles <see cref="GlobalSearchQuery"/>. A READ-side handler that searches across TWO sources the caller
/// can see — their projects and the tasks inside those projects — normalizes each match into the uniform
/// <see cref="SearchResultItem"/>, then returns ONE ordered, paged slice of the union. It never materializes
/// aggregates; every branch projects straight into the flat result record so only display columns cross the
/// wire.
/// </summary>
/// <remarks>
/// WHY PROJECT EACH SOURCE INTO A COMMON SHAPE, THEN CONCAT AND PAGE IN THE DATABASE?
/// Building two <c>IQueryable&lt;SearchResultItem&gt;</c> shapes and joining them with <see cref="Queryable.Concat"/>
/// lets EF Core emit a single SQL statement (a UNION ALL) so ordering, <c>Skip</c>/<c>Take</c> and the count all run
/// in the database — never in memory. If we materialized each source and merged in C#, we'd over-fetch both tables
/// and lose deterministic, index-backed paging.
///
/// WHY IS SCOPE ENFORCED IN EVERY BRANCH?
/// Each source query is independently filtered to the caller's memberships. Security lives with the data access,
/// not in a single up-front gate, so a future third source can't accidentally bypass it.
/// </remarks>
public sealed class GlobalSearchQueryHandler
    : IQueryHandler<GlobalSearchQuery, PagedList<SearchResultItem>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<GlobalSearchQueryHandler> _logger;

    public GlobalSearchQueryHandler(
        IApplicationDbContext context,
        ICurrentUser currentUser,
        ILogger<GlobalSearchQueryHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<PagedList<SearchResultItem>>> Handle(
        GlobalSearchQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Resolve the caller. Search is scoped to membership, so an anonymous request has nothing it
        //    could legitimately match — fail fast with 401 before composing any SQL.
        if (_currentUser.UserId is not { } userId)
        {
            _logger.LogWarning("GlobalSearch reached the handler without an authenticated user.");
            return Result.Failure<PagedList<SearchResultItem>>(Error.Unauthorized(
                "Search.Unauthenticated",
                "You must be signed in to search."));
        }

        // 2. Normalize the term once and build the LIKE pattern. Trimmed here (not in the DB) so the SQL
        //    parameter is clean and the same pattern feeds every branch.
        var term = request.SearchTerm.Trim();
        var pattern = $"%{term}%";

        // 3. Projects the caller belongs to whose name/description matches. Projected into the common shape;
        //    for a project hit Id and ProjectId are the same value (see SearchResultItem remarks).
        var projectHits = _context.Projects
            .AsNoTracking()
            .Where(p => p.Members.Any(m => m.UserId == userId))
            .Where(p =>
                EF.Functions.Like(p.Name.Value, pattern) ||
                (p.Description != null && EF.Functions.Like(p.Description, pattern)))
            .Select(p => new SearchResultItem(
                SearchResultType.Project,
                p.Id,
                p.Id,
                p.Name.Value,
                p.Description,
                p.CreatedAtUtc));

        // 4. Tasks inside the caller's projects whose title/description matches. The EXISTS keeps the
        //    membership check on the parent project without a join that could duplicate rows.
        var taskHits = _context.ProjectTasks
            .AsNoTracking()
            .Where(t => _context.Projects.Any(
                p => p.Id == t.ProjectId && p.Members.Any(m => m.UserId == userId)))
            .Where(t =>
                EF.Functions.Like(t.Title.Value, pattern) ||
                (t.Description != null && EF.Functions.Like(t.Description, pattern)))
            .Select(t => new SearchResultItem(
                SearchResultType.Task,
                t.Id,
                t.ProjectId,
                t.Title.Value,
                t.Description,
                t.CreatedAtUtc));

        // 5. Union the two shapes. Concat -> UNION ALL: we already know the sets are disjoint (a project id
        //    can never equal a task id in practice and the Type tag differs), so we skip the DISTINCT cost.
        var union = projectHits.Concat(taskHits);

        // 6. Total across the whole union BEFORE paging — the denominator for page-count math. Runs as a
        //    COUNT over the UNION ALL, still one round-trip.
        var totalCount = await union.CountAsync(cancellationToken);

        // 7. Order newest-first (Id tiebreaker for stable pages across duplicate timestamps), slice to the
        //    page, and materialize. Ordering/paging execute in SQL over the union.
        var items = await union
            .OrderByDescending(r => r.CreatedAtUtc)
            .ThenBy(r => r.Id)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Global search for user {UserId} matched {Total} items (term length {Length}, page {Page}).",
            userId, totalCount, term.Length, request.PageNumber);

        return new PagedList<SearchResultItem>(
            items, totalCount, request.PageNumber, request.PageSize);
    }
}
