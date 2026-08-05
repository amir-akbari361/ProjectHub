using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Application.Common;

namespace ProjectHub.Application.Features.Notifications.MarkAllAsRead;

/// <summary>
/// Handles <see cref="MarkAllNotificationsAsReadCommand"/>. A WRITE-side handler that loads the caller's
/// UNREAD notifications TRACKED, walks them through the same <c>Notification.MarkAsRead</c> domain method
/// each single mark uses, and commits ONCE for the whole batch.
/// </summary>
/// <remarks>
/// WHY LOAD-AND-LOOP INSTEAD OF EF's <c>ExecuteUpdate</c> (a set-based UPDATE)?
/// <c>ExecuteUpdate</c> is one SQL statement and never loads rows — very fast — but it BYPASSES the domain
/// entirely: no <c>NotificationReadDomainEvent</c> is raised, no <c>UpdatedAt</c>/audit bookkeeping from
/// our interceptors runs, and it commits immediately outside the Unit of Work. For consistency with the
/// single-mark path and to keep domain events firing, we deliberately choose the aggregate route. We
/// filter to UNREAD first so the batch is naturally bounded (already-read rows are skipped, not reloaded).
/// If a user could ever accumulate tens of thousands of unread rows, this is the seam where we'd revisit
/// the trade-off — YAGNI until the numbers say otherwise.
///
/// WHY IS THIS ALWAYS A SUCCESS, EVEN WITH ZERO UNREAD?
/// "Mark all as read" is idempotent by nature: an empty inbox is already in the desired state. Returning
/// success with nothing to do is the correct, surprise-free contract for a "clear the badge" button.
/// </remarks>
public sealed class MarkAllNotificationsAsReadCommandHandler
    : ICommandHandler<MarkAllNotificationsAsReadCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<MarkAllNotificationsAsReadCommandHandler> _logger;

    public MarkAllNotificationsAsReadCommandHandler(
        IApplicationDbContext context,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork,
        ILogger<MarkAllNotificationsAsReadCommandHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> Handle(
        MarkAllNotificationsAsReadCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Resolve the caller. A personal bulk action with no principal => 401.
        if (_currentUser.UserId is not { } userId)
        {
            _logger.LogWarning("MarkAllNotificationsAsRead reached the handler without an authenticated user.");
            return Result.Failure(Error.Unauthorized(
                "Notifications.Unauthenticated",
                "You must be signed in to update notifications."));
        }

        // 2. Load ONLY this user's UNREAD notifications, TRACKED. Filtering by !IsRead bounds the batch to
        //    exactly the rows that will actually change — already-read rows are never materialized.
        var unread = await _context.Notifications
            .Where(n => n.RecipientId == userId && !n.IsRead)
            .ToListAsync(cancellationToken);

        // 3. Nothing to do => success (idempotent). Avoids a pointless SaveChanges round-trip.
        if (unread.Count == 0)
        {
            _logger.LogInformation(
                "MarkAllNotificationsAsRead: user {UserId} had no unread notifications.", userId);
            return Result.Success();
        }

        // 4. Walk each through the SAME domain method the single-mark path uses. One timestamp for the
        //    whole batch so they share a consistent ReadAtUtc, and each raises its own domain event.
        var now = _dateTimeProvider.UtcNow;
        foreach (var notification in unread)
        {
            notification.MarkAsRead(now);
        }

        // 5. Commit the whole batch in ONE transaction.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Marked {Count} notifications as read for user {UserId}.", unread.Count, userId);

        return Result.Success();
    }
}
