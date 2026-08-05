using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Application.Common;

namespace ProjectHub.Application.Features.Notifications.MarkAsRead;

/// <summary>
/// Handles <see cref="MarkNotificationAsReadCommand"/>. A WRITE-side handler that loads the caller's OWN
/// notification TRACKED, delegates the state change to <c>Notification.MarkAsRead</c> — which is idempotent
/// and raises <c>NotificationReadDomainEvent</c> only on a real transition — and commits once.
/// </summary>
/// <remarks>
/// WHY FILTER BY RecipientId IN THE LOAD ITSELF?
/// Combining "this id" AND "belongs to me" in a single WHERE means a notification owned by someone else
/// returns null, which we translate into the SAME 404 as a missing one. Ownership is enforced at the query,
/// so there is no separate authorization branch to forget and no information disclosure.
///
/// WHY IS THE IDEMPOTENCY IN THE DOMAIN, NOT HERE?
/// <c>Notification.MarkAsRead</c> returns early if already read (no event, no timestamp change). Marking an
/// already-read notification is therefore a no-op that still returns success — the client gets a
/// predictable 204 whether or not this was the first read, which is exactly what a "mark as read" button
/// wants.
/// </remarks>
public sealed class MarkNotificationAsReadCommandHandler
    : ICommandHandler<MarkNotificationAsReadCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<MarkNotificationAsReadCommandHandler> _logger;

    public MarkNotificationAsReadCommandHandler(
        IApplicationDbContext context,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork,
        ILogger<MarkNotificationAsReadCommandHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> Handle(
        MarkNotificationAsReadCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Resolve the caller. Marking is a personal, attributed action, so no principal => 401.
        if (_currentUser.UserId is not { } userId)
        {
            _logger.LogWarning("MarkNotificationAsRead reached the handler without an authenticated user.");
            return Result.Failure(Error.Unauthorized(
                "Notifications.Unauthenticated",
                "You must be signed in to update notifications."));
        }

        // 2. Load the notification TRACKED, filtered to the caller's own rows. "Not mine" and "not found"
        //    collapse into a single null -> 404 (no disclosure that it exists for someone else).
        var notification = await _context.Notifications
            .SingleOrDefaultAsync(
                n => n.Id == request.NotificationId && n.RecipientId == userId,
                cancellationToken);

        if (notification is null)
        {
            _logger.LogInformation(
                "MarkNotificationAsRead: notification {NotificationId} not found for user {UserId}.",
                request.NotificationId, userId);
            return Result.Failure(NotificationErrors.NotFound(request.NotificationId));
        }

        // 3. Delegate to the domain. Idempotent by design — a no-op if already read. No try/catch: there is
        //    no invariant that can be violated here (ownership was proven by the query above).
        notification.MarkAsRead(_dateTimeProvider.UtcNow);

        // 4. Commit once. If it was already read, EF detects no changes and this is a cheap no-op.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Notification {NotificationId} marked as read by user {UserId}.",
            request.NotificationId, userId);

        return Result.Success();
    }
}
