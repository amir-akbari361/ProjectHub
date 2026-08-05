using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Application.Common;

namespace ProjectHub.Application.Features.Notifications.ListNotifications;

/// <summary>
/// Handles <see cref="ListNotificationsQuery"/>. A READ-side handler that binds the recipient to the
/// AUTHENTICATED caller, optionally filters to unread, then composes a single paged SQL statement over
/// that user's notifications, projecting straight into <see cref="NotificationResponse"/>. It NEVER
/// materializes a <c>Notification</c> aggregate — the read side stays free of domain invariants and
/// change tracking.
/// </summary>
/// <remarks>
/// WHY NEWEST-FIRST?
/// An inbox is scanned top-down for the latest events, so we order DESCENDING by creation time with Id as
/// a stable tiebreaker — the opposite of a comment thread (a conversation read oldest-first).
///
/// WHY IS THE RECIPIENT NEVER A PARAMETER?
/// The WHERE clause pins every row to <c>RecipientId == userId</c> taken from the token. There is no way
/// for a caller to page another user's inbox — the query is secure by construction against IDOR.
/// </remarks>
public sealed class ListNotificationsQueryHandler
    : IQueryHandler<ListNotificationsQuery, PagedList<NotificationResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<ListNotificationsQueryHandler> _logger;

    public ListNotificationsQueryHandler(
        IApplicationDbContext context,
        ICurrentUser currentUser,
        ILogger<ListNotificationsQueryHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<PagedList<NotificationResponse>>> Handle(
        ListNotificationsQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Resolve the caller. An inbox is inherently personal, so an unauthenticated request has
        //    nothing it could legitimately see — fail fast with 401 before touching the DB.
        if (_currentUser.UserId is not { } userId)
        {
            _logger.LogWarning("ListNotifications reached the handler without an authenticated user.");
            return Result.Failure<PagedList<NotificationResponse>>(Error.Unauthorized(
                "Notifications.Unauthenticated",
                "You must be signed in to view notifications."));
        }

        // 2. Base query: THIS user's notifications. AsNoTracking() — pure read; the global soft-delete
        //    filter already excludes deleted rows. Binding RecipientId to the token is the whole
        //    authorization story for this feature (no project/task scoping applies).
        var query = _context.Notifications
            .AsNoTracking()
            .Where(n => n.RecipientId == userId);

        // 3. Optional filter. "Unread only" is a WHERE over the SAME read model — used by the badge/inbox
        //    view that shows just actionable items.
        if (request.UnreadOnly)
        {
            query = query.Where(n => !n.IsRead);
        }

        // 4. Total BEFORE paging — the denominator for page-count math (respects the UnreadOnly filter).
        var totalCount = await query.CountAsync(cancellationToken);

        // 5. Order newest-first (Id tiebreaker for stable pages), slice to the page, and project into the
        //    lean DTO in the SAME query so EF emits one round-trip.
        var items = await query
            .OrderByDescending(n => n.CreatedAtUtc)
            .ThenBy(n => n.Id)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(n => new NotificationResponse(
                n.Id,
                n.Type,
                n.Message,
                n.IsRead,
                n.CreatedAtUtc,
                n.ReadAtUtc))
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Listed {Count} of {Total} notifications for user {UserId} (page {Page}, unreadOnly={UnreadOnly}).",
            items.Count, totalCount, userId, request.PageNumber, request.UnreadOnly);

        return new PagedList<NotificationResponse>(
            items, totalCount, request.PageNumber, request.PageSize);
    }
}
