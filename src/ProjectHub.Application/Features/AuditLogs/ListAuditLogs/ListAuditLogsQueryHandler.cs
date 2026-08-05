using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Application.Common;

namespace ProjectHub.Application.Features.AuditLogs.ListAuditLogs;

/// <summary>
/// Handles <see cref="ListAuditLogsQuery"/>. A READ-side handler that pages the immutable audit trail
/// for a single entity, newest-first, projecting straight into <see cref="AuditLogResponse"/>. It never
/// materializes the <c>AuditLog</c> aggregate — the read side stays free of change tracking and domain
/// invariants. Authorization is enforced up front: only a signed-in caller may read a trail.
/// </summary>
/// <remarks>
/// WHY IS AUTHORIZATION HERE ONLY "MUST BE SIGNED IN" AND NOT MEMBERSHIP-SCOPED?
/// The audit store is polymorphic (any entity type by name), so this handler cannot cheaply resolve the
/// owning project for an arbitrary (EntityName, EntityId) to check membership without a big switch over
/// entity types. In this build the trail is gated behind authentication and the entity-name allow-list
/// in the validator; a hardening pass would add per-entity ownership resolution. Documenting the gap is
/// deliberate — it's a known, bounded trade-off, not an oversight.
///
/// WHY NEWEST-FIRST WITH AN Id TIEBREAKER?
/// A history timeline is read latest-change-first, and audit rows written in the same millisecond need a
/// deterministic order so pages don't shuffle — Id provides that stable secondary sort.
/// </remarks>
public sealed class ListAuditLogsQueryHandler
    : IQueryHandler<ListAuditLogsQuery, PagedList<AuditLogResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<ListAuditLogsQueryHandler> _logger;

    public ListAuditLogsQueryHandler(
        IApplicationDbContext context,
        ICurrentUser currentUser,
        ILogger<ListAuditLogsQueryHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<PagedList<AuditLogResponse>>> Handle(
        ListAuditLogsQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Gate on authentication. An audit trail is an internal record — an anonymous caller has no
        //    legitimate view of it, so fail closed with 401 before touching the DB.
        if (_currentUser.UserId is null)
        {
            _logger.LogWarning("ListAuditLogs reached the handler without an authenticated user.");
            return Result.Failure<PagedList<AuditLogResponse>>(Error.Unauthorized(
                "AuditLog.Unauthenticated",
                "You must be signed in to view audit history."));
        }

        // 2. Base query pinned to the requested entity. The (EntityName, EntityId) pair is the composite
        //    key the writer used, and it's index-backed for fast timeline retrieval. AsNoTracking() — this
        //    is a pure read that never mutates or re-saves the rows.
        var query = _context.AuditLogs
            .AsNoTracking()
            .Where(a => a.EntityName == request.EntityName && a.EntityId == request.EntityId);

        // 3. Total BEFORE paging — the denominator for page-count math over the full trail.
        var totalCount = await query.CountAsync(cancellationToken);

        // 4. Order newest-first (Id tiebreaker for stable pages), slice to the page, and project into the
        //    lean DTO in the SAME query so EF emits a single round-trip.
        var items = await query
            .OrderByDescending(a => a.CreatedAtUtc)
            .ThenBy(a => a.Id)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(a => new AuditLogResponse(
                a.Id,
                a.EntityName,
                a.EntityId,
                a.Action,
                a.PerformedBy,
                a.Changes,
                a.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Listed {Count} of {Total} audit entries for {EntityName} {EntityId} (page {Page}).",
            items.Count, totalCount, request.EntityName, request.EntityId, request.PageNumber);

        return new PagedList<AuditLogResponse>(
            items, totalCount, request.PageNumber, request.PageSize);
    }
}
