using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Domain.Events;

namespace ProjectHub.Application.Features.Notifications.EventHandlers;

/// <summary>
/// Pushes a newly created notification to its recipient's live connections, turning the inbox real-time.
/// <c>Notification.Create</c> raises <see cref="NotificationCreatedDomainEvent"/> unconditionally, so this
/// single handler covers EVERY source of notifications — the five event handlers in this folder today, plus
/// anything added later — without those producers knowing that a push exists.
/// </summary>
/// <remarks>
/// WHY THE COUNT IS QUERIED RATHER THAN TRACKED.
/// The publish seam runs POST-commit, so the new row is already visible to this query; counting in the
/// database (instead of incrementing a number in memory) means the pushed badge value matches exactly what
/// a page refresh would show, even when several notifications commit in the same unit of work or another
/// device marks some read concurrently.
///
/// WHY THE COUNT SEMANTICS ARE DUPLICATED HERE.
/// The predicate below deliberately mirrors <c>ListNotificationsQueryHandler</c>'s unread filter
/// (recipient + <c>!IsRead</c>, with the context's global soft-delete filter applying to both). The badge
/// must never disagree with the inbox the user then opens.
///
/// WHY THE TRY/CATCH.
/// MediatR pipeline behaviors wrap <c>IRequestHandler</c> only — a notification handler that throws would
/// bubble out of the publish seam and fail an ALREADY-COMMITTED operation. A push is a best-effort
/// optimization over a durable row, so a transport fault is logged and swallowed; the client resyncs its
/// count on reconnect (see the hub client's reconnect handler).
/// </remarks>
public sealed class NotificationPushHandler
    : INotificationHandler<DomainEventNotification<NotificationCreatedDomainEvent>>
{
    private readonly IApplicationDbContext _context;
    private readonly INotificationPusher _pusher;
    private readonly ILogger<NotificationPushHandler> _logger;

    public NotificationPushHandler(
        IApplicationDbContext context,
        INotificationPusher pusher,
        ILogger<NotificationPushHandler> logger)
    {
        _context = context;
        _pusher = pusher;
        _logger = logger;
    }

    public async Task Handle(
        DomainEventNotification<NotificationCreatedDomainEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        try
        {
            // The event carries the id, recipient, and type but not the text, so read the message back.
            // A null means the row is gone (soft-deleted in a racing operation) — nothing to announce.
            var message = await _context.Notifications
                .AsNoTracking()
                .Where(entity => entity.Id == domainEvent.NotificationId)
                .Select(entity => entity.Message)
                .SingleOrDefaultAsync(cancellationToken);

            if (message is null)
            {
                return;
            }

            var unreadCount = await _context.Notifications
                .AsNoTracking()
                .CountAsync(
                    entity => entity.RecipientId == domainEvent.RecipientId && !entity.IsRead,
                    cancellationToken);

            await _pusher.PushAsync(
                domainEvent.RecipientId,
                new NotificationPush(
                    domainEvent.NotificationId,
                    domainEvent.Type.ToString(),
                    message,
                    domainEvent.OccurredAtUtc,
                    unreadCount),
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to push notification {NotificationId} to recipient {RecipientId}.",
                domainEvent.NotificationId,
                domainEvent.RecipientId);
        }
    }
}
