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

        // WHY BOTH SIDES PROJECT INTO AN ANONYMOUS TYPE INSTEAD OF DIRECTLY INTO SearchResultItem
        // ---------------------------------------------------------------------------------------
        // A set operation (Concat -> UNION ALL) requires EF to align the two SELECT expressions column for
        // column. Projecting straight into `new SearchResultItem(...)` — a positional record — is a CLIENT
        // projection: EF reads the columns and then invokes the constructor in memory, which leaves it nothing
        // to align, and it throws "Unable to translate set operation after client projection has been applied".
        //
        // An anonymous type (or any member-init shape) is handled differently: EF pushes each member down into
        // the SelectExpression and reconstructs the object after reading the row. The projection therefore stays
        // server-side and alignable, so the UNION translates. The record is then built from the materialized rows
        // in step 7 — after SQL has done the union, the count, the ordering and the paging.
        //
        // The alternative, running the two queries separately and merging in memory, would be much worse: paging
        // and the total count both have to span the union, so it would mean fetching every match from both tables
        // on every keystroke just to return twenty rows.

        // 3. Projects the caller belongs to whose name/description matches. For a project hit Id and ProjectId
        //    are the same value (see SearchResultItem remarks).
        var projectHits = _context.Projects
            .AsNoTracking()
            .Where(p => p.Members.Any(m => m.UserId == userId))
            .Where(p =>
                EF.Functions.Like(p.Name.Value, pattern) ||
                (p.Description != null && EF.Functions.Like(p.Description, pattern)))
            .Select(p => new
            {
                Type = SearchResultType.Project,
                Id = p.Id,
                ProjectId = p.Id,
                Title = p.Name.Value,
                Description = p.Description,
                CreatedAtUtc = p.CreatedAtUtc
            });

        // 4. Tasks inside the caller's projects whose title/description matches. The EXISTS keeps the
        //    membership check on the parent project without a join that could duplicate rows.
        //
        //    The member names, types and ORDER must match projectHits exactly — that is what makes both sides
        //    the same anonymous type, and therefore what makes them unionable.
        var taskHits = _context.ProjectTasks
            .AsNoTracking()
            .Where(t => _context.Projects.Any(
                p => p.Id == t.ProjectId && p.Members.Any(m => m.UserId == userId)))
            .Where(t =>
                EF.Functions.Like(t.Title.Value, pattern) ||
                (t.Description != null && EF.Functions.Like(t.Description, pattern)))
            .Select(t => new
            {
                Type = SearchResultType.Task,
                Id = t.Id,
                ProjectId = t.ProjectId,
                Title = t.Title.Value,
                Description = t.Description,
                CreatedAtUtc = t.CreatedAtUtc
            });

        // 5. Union the two shapes. Concat -> UNION ALL: the sets are disjoint by construction (a row from one
        //    branch can never equal a row from the other, because the Type tag differs), so we skip the cost of
        //    the DISTINCT that Union would impose.
        var union = projectHits.Concat(taskHits);

        // 6. Total across the whole union BEFORE paging — the denominator for page-count math. Runs as a
        //    COUNT over the UNION ALL, still one round-trip.
        var totalCount = await union.CountAsync(cancellationToken);

        // 7. Order newest-first (Id tiebreaker for stable pages across duplicate timestamps), slice to the
        //    page, then map to the response record. Ordering and paging execute in SQL over the union; only the
        //    twenty rows of the requested page are ever materialized.
        var rows = await union
            .OrderByDescending(row => row.CreatedAtUtc)
            .ThenBy(row => row.Id)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(row => new SearchResultItem(
                row.Type,
                row.Id,
                row.ProjectId,
                row.Title,
                row.Description,
                row.CreatedAtUtc))
            .ToList();

        _logger.LogInformation(
            "Global search for user {UserId} matched {Total} items (term length {Length}, page {Page}).",
            userId, totalCount, term.Length, request.PageNumber);

        return new PagedList<SearchResultItem>(
            items, totalCount, request.PageNumber, request.PageSize);
    }
}
