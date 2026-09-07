using MediatR;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Domain.Abstractions;
using ProjectHub.Domain.Entities;

namespace ProjectHub.Application.Features.Notifications.EventHandlers;

/// <summary>
/// Base for the MediatR handlers that turn a committed domain event into user notifications. It owns the
/// cross-cutting mechanics every notification handler shares — dropping the actor, de-duplicating, skipping
/// an empty set, materialising <see cref="Notification"/> aggregates, and committing — so each concrete
/// handler answers only one question: "given this event, who should be told what?".
/// </summary>
/// <remarks>
/// WHERE DOES THIS RUN?
/// <c>PublishDomainEventsInterceptor</c> dispatches domain events AFTER the originating transaction commits,
/// but still inside the same request scope. So the scoped <see cref="ICurrentUser"/> is populated (the actor
/// is known) and the <see cref="IUnitOfWork.SaveChangesAsync"/> below is a NEW, post-commit unit of work.
/// Persisting the notification re-enters the same interceptor and raises <c>NotificationCreatedDomainEvent</c>,
/// the single choke-point the SignalR push (Part G) subscribes to. That recursion terminates: the push
/// handler performs no further save. <c>Notification</c> is not in the audit allow-list, so it produces no
/// audit rows either.
///
/// WHY SWALLOW FAILURES?
/// A notification is a side effect of an action that ALREADY succeeded and committed. If resolving recipients
/// or saving the notification fails, the user's task-assignment / comment / sprint-start must still be reported
/// as the success it was — so a failure here is logged and contained, never rethrown into the completed
/// operation (which would surface as a spurious 500 to the client).
///
/// WHY EXCLUDE THE ACTOR UNIFORMLY?
/// Nobody needs to be told about a thing they just did — assigning yourself a task, commenting on your own
/// task, adding yourself when you create a project. Removing <see cref="ICurrentUser.UserId"/> from the
/// resolved set enforces that once, for every handler, so a resolver may return the natural recipient set
/// without pre-filtering the actor out.
/// </remarks>
public abstract class NotificationEventHandler<TDomainEvent>
    : INotificationHandler<DomainEventNotification<TDomainEvent>>
    where TDomainEvent : IDomainEvent
{
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger _logger;

    protected NotificationEventHandler(
        IApplicationDbContext context,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork,
        ILogger logger)
    {
        Context = context;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>The write context, exposed to subclasses for their recipient-resolution reads.</summary>
    protected IApplicationDbContext Context { get; }

    public async Task Handle(
        DomainEventNotification<TDomainEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        try
        {
            var requests = await ResolveAsync(domainEvent, cancellationToken);
            if (requests.Count == 0)
            {
                return;
            }

            // Drop the actor (nobody is notified of their own action) and any empty/duplicate recipient so a
            // user is notified at most once per event. A null actor (seeder/system) excludes nobody.
            var actor = _currentUser.UserId;
            var recipients = requests
                .Where(request => request.RecipientId != Guid.Empty && request.RecipientId != actor)
                .GroupBy(request => request.RecipientId)
                .Select(group => group.First())
                .ToList();

            if (recipients.Count == 0)
            {
                return;
            }

            var utcNow = _dateTimeProvider.UtcNow;
            foreach (var request in recipients)
            {
                var entity = Notification.Create(
                    request.RecipientId, request.Type, request.Message, utcNow);
                Context.Notifications.Add(entity);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Generated {Count} {EventType} notification(s).",
                recipients.Count, typeof(TDomainEvent).Name);
        }
        catch (Exception exception)
        {
            // Contain the failure: the originating action already committed and must not be reported as
            // failed just because its notification could not be produced.
            _logger.LogError(
                exception,
                "Failed to generate notifications for {EventType}.",
                typeof(TDomainEvent).Name);
        }
    }

    /// <summary>
    /// Resolves the intended recipients and their messages for this event. Return an empty collection when
    /// there is nobody to notify (e.g. an unassigned task, or a deleted parent). The actor is removed and
    /// duplicates are collapsed by the base class, so implementations may return the natural set without
    /// filtering for those cases.
    /// </summary>
    protected abstract Task<IReadOnlyCollection<NotificationRequest>> ResolveAsync(
        TDomainEvent domainEvent,
        CancellationToken cancellationToken);
}
