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
/// invariants. Access is enforced up front: the caller must be signed in AND either belong to the
/// owning project or hold the global Admin role.
/// </summary>
/// <remarks>
/// HOW IS ACCESS SCOPED WITHOUT A PROJECT ID ON THE QUERY?
/// The audit store is polymorphic (any entity type by name), so the owning project is resolved from the
/// LIVE entity — a small switch over the allow-listed entity names maps (EntityName, EntityId) to its
/// project. We deliberately resolve from the entity, NOT from a ProjectId on the audit rows: the writer
/// only stamps rows going forward, so a pre-existing or seeded record has an empty trail, and keying the
/// check off the trail would wrongly 404 a project a member legitimately owns. A caller who is neither a
/// member nor an Admin gets the SAME <see cref="AuditLogErrors.EntityNotFound"/> as a caller asking for a
/// non-existent entity, so membership can't be probed by watching for a different error.
///
/// WHY DOES ADMIN BYPASS MEMBERSHIP?
/// Admin's remit is oversight — the global audit viewer — so an Admin may read any entity's trail without
/// joining its project. This is the same role the <c>Admin</c> authorization policy gates the admin-only
/// endpoints with; here it is a handler-side check via <see cref="ICurrentUser.IsAdmin"/>.
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

        var userId = _currentUser.UserId.Value;

        // 2. Resolve the owning project from the LIVE entity (not the trail — a pre-existing record has
        //    no rows yet). A name outside the allow-list, or an id that resolves to no entity, yields
        //    null → EntityNotFound, which is also what a non-member receives, so existence stays opaque.
        var owningProjectId = await ResolveOwningProjectIdAsync(request, cancellationToken);
        if (owningProjectId is null)
        {
            _logger.LogWarning(
                "Audit trail for {EntityName} {EntityId} could not be resolved to an owning project.",
                request.EntityName, request.EntityId);
            return Result.Failure<PagedList<AuditLogResponse>>(
                AuditLogErrors.EntityNotFound(request.EntityName, request.EntityId));
        }

        // 3. Membership gate. Admins have oversight and may read any trail; everyone else must belong to
        //    the owning project. Denial returns the SAME not-found error as a missing entity so a
        //    non-member cannot distinguish "forbidden" from "doesn't exist".
        if (!_currentUser.IsAdmin)
        {
            var isMember = await _context.Projects
                .AsNoTracking()
                .AnyAsync(
                    p => p.Id == owningProjectId.Value && p.Members.Any(m => m.UserId == userId),
                    cancellationToken);

            if (!isMember)
            {
                _logger.LogWarning(
                    "User {UserId} was denied the audit trail for {EntityName} {EntityId} (not a project member).",
                    userId, request.EntityName, request.EntityId);
                return Result.Failure<PagedList<AuditLogResponse>>(
                    AuditLogErrors.EntityNotFound(request.EntityName, request.EntityId));
            }
        }

        // 4. Base query pinned to the requested entity. The (EntityName, EntityId) pair is the composite
        //    key the writer used, and it's index-backed for fast timeline retrieval. AsNoTracking() — this
        //    is a pure read that never mutates or re-saves the rows.
        var query = _context.AuditLogs
            .AsNoTracking()
            .Where(a => a.EntityName == request.EntityName && a.EntityId == request.EntityId);

        // 5. Total BEFORE paging — the denominator for page-count math over the full trail.
        var totalCount = await query.CountAsync(cancellationToken);

        // 6. Order newest-first (Id tiebreaker for stable pages), slice to the page, and project into the
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

    // Maps an allow-listed (EntityName, EntityId) to its owning project id by inspecting the live entity.
    // Returns null when the name is unrecognised or no such entity exists — the caller treats both as
    // "not found". Names are matched case-insensitively to mirror the validator's allow-list. ProjectMember
    // is reached through the Project.Members navigation because the context exposes only aggregate roots.
    private Task<Guid?> ResolveOwningProjectIdAsync(
        ListAuditLogsQuery request,
        CancellationToken cancellationToken)
    {
        var entityId = request.EntityId;

        return request.EntityName.ToLowerInvariant() switch
        {
            "project" => _context.Projects
                .Where(p => p.Id == entityId)
                .Select(p => (Guid?)p.Id)
                .FirstOrDefaultAsync(cancellationToken),

            "projecttask" => _context.ProjectTasks
                .Where(t => t.Id == entityId)
                .Select(t => (Guid?)t.ProjectId)
                .FirstOrDefaultAsync(cancellationToken),

            "sprint" => _context.Sprints
                .Where(s => s.Id == entityId)
                .Select(s => (Guid?)s.ProjectId)
                .FirstOrDefaultAsync(cancellationToken),

            "projectmember" => _context.Projects
                .Where(p => p.Members.Any(m => m.Id == entityId))
                .Select(p => (Guid?)p.Id)
                .FirstOrDefaultAsync(cancellationToken),

            "comment" => _context.ProjectTasks
                .Where(t => _context.Comments.Any(c => c.Id == entityId && c.TaskId == t.Id))
                .Select(t => (Guid?)t.ProjectId)
                .FirstOrDefaultAsync(cancellationToken),

            "attachment" => _context.ProjectTasks
                .Where(t => _context.Attachments.Any(a => a.Id == entityId && a.TaskId == t.Id))
                .Select(t => (Guid?)t.ProjectId)
                .FirstOrDefaultAsync(cancellationToken),

            _ => Task.FromResult<Guid?>(null),
        };
    }
}
