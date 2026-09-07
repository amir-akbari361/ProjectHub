using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Application.Common;

namespace ProjectHub.Application.Features.AuditLogs.ListAllAuditLogs;

/// <summary>
/// Handles <see cref="ListAllAuditLogsQuery"/> — the Admin-only, organisation-wide audit trail. Applies the
/// optional filters, pages newest-first, and projects into <see cref="AdminAuditLogResponse"/> with the
/// project name and performer email resolved so the view is readable without a second round-trip.
/// </summary>
/// <remarks>
/// WHY CHECK THE ROLE HERE WHEN THE ENDPOINT IS ALREADY GATED BY THE "Admin" POLICY?
/// Defence in depth, and it is the same belt-and-braces the membership-scoped sibling applies. The policy
/// protects the HTTP route; this protects the QUERY. Anything that dispatches it later — a background job, a
/// new endpoint, a test — cannot accidentally hand a non-admin the whole organisation's activity because the
/// authorisation lives with the behaviour rather than with one transport.
///
/// WHY 403 RATHER THAN THE 404 ITS SIBLING USES?
/// The per-entity query hides existence deliberately: a distinct error there would let an outsider probe
/// whether a given task exists. Here there is no per-entity secret to protect — the route is a documented
/// admin feature — so a plain "Admin only" is more honest and easier to diagnose than a fake 404.
///
/// WHY SUBQUERIES FOR THE NAMES INSTEAD OF JOINS?
/// Both lookups are optional-to-nothing (a null project, a system actor, a soft-deleted row), which is
/// exactly LEFT JOIN semantics. Expressing them as correlated subqueries inside the projection lets EF emit
/// them as OUTER APPLYs in the SAME statement — one round-trip, and a missing row yields null instead of
/// dropping the audit entry, which an inner join would do.
/// </remarks>
public sealed class ListAllAuditLogsQueryHandler
    : IQueryHandler<ListAllAuditLogsQuery, PagedList<AdminAuditLogResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<ListAllAuditLogsQueryHandler> _logger;

    public ListAllAuditLogsQueryHandler(
        IApplicationDbContext context,
        ICurrentUser currentUser,
        ILogger<ListAllAuditLogsQueryHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<PagedList<AdminAuditLogResponse>>> Handle(
        ListAllAuditLogsQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Authentication, then role. Kept as two checks so an anonymous caller gets 401 (log in) while a
        //    signed-in non-admin gets 403 (you are known, but this is not yours) — different remedies.
        if (_currentUser.UserId is not { } userId)
        {
            _logger.LogWarning("ListAllAuditLogs reached the handler without an authenticated user.");
            return Result.Failure<PagedList<AdminAuditLogResponse>>(Error.Unauthorized(
                "AuditLog.Unauthenticated",
                "You must be signed in to view audit history."));
        }

        if (!_currentUser.IsAdmin)
        {
            _logger.LogWarning(
                "User {UserId} was denied the organisation-wide audit trail (not an Admin).", userId);
            return Result.Failure<PagedList<AdminAuditLogResponse>>(AuditLogErrors.AdminOnly);
        }

        // 2. Start from every row and narrow by whichever filters were supplied. AsNoTracking() — the audit
        //    store is append-only and this is a pure read.
        var query = _context.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.EntityName))
        {
            // The validator has already confirmed the name is allow-listed. Compare to the trimmed value so
            // a stray space from a query string cannot silently match nothing.
            var entityName = request.EntityName.Trim();
            query = query.Where(a => a.EntityName == entityName);
        }

        if (request.ProjectId is { } projectId)
        {
            query = query.Where(a => a.ProjectId == projectId);
        }

        if (request.PerformedBy is { } performedBy)
        {
            query = query.Where(a => a.PerformedBy == performedBy);
        }

        // Both bounds inclusive: see the query's remarks on how the UI's two date pickers map onto them.
        if (request.FromUtc is { } fromUtc)
        {
            query = query.Where(a => a.CreatedAtUtc >= fromUtc);
        }

        if (request.ToUtc is { } toUtc)
        {
            query = query.Where(a => a.CreatedAtUtc <= toUtc);
        }

        // 3. Total BEFORE paging, over the filtered set — the denominator the client pages against.
        var totalCount = await query.CountAsync(cancellationToken);

        // 4. Newest-first with an Id tiebreaker so rows written in the same millisecond keep a stable order
        //    across pages, then slice and project in one statement.
        var items = await query
            .OrderByDescending(a => a.CreatedAtUtc)
            .ThenBy(a => a.Id)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(a => new AdminAuditLogResponse(
                a.Id,
                a.EntityName,
                a.EntityId,
                a.Action,
                a.ProjectId,
                _context.Projects
                    .Where(project => project.Id == a.ProjectId)
                    .Select(project => project.Name.Value)
                    .FirstOrDefault(),
                a.PerformedBy,
                _context.Users
                    .Where(user => user.Id == a.PerformedBy)
                    .Select(user => user.Email.Value)
                    .FirstOrDefault(),
                a.Changes,
                a.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Admin {UserId} listed {Count} of {Total} audit entries (page {Page}).",
            userId, items.Count, totalCount, request.PageNumber);

        return new PagedList<AdminAuditLogResponse>(
            items, totalCount, request.PageNumber, request.PageSize);
    }
}
