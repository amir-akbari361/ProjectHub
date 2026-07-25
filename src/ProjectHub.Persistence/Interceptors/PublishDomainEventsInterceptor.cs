using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Domain.Abstractions;
using ProjectHub.Domain.Primitives;

namespace ProjectHub.Persistence.Interceptors;

/// <summary>
/// Collects the domain events raised by aggregates during a unit of work and publishes them
/// through MediatR after the transaction commits. Hooking <see cref="SavingChangesAsync"/> keeps
/// event dispatch tied to a successful persist — handlers never fire for changes that were rolled
/// back. Wrapping each event in <see cref="DomainEventNotification{TDomainEvent}"/> lets the Domain
/// stay free of any MediatR dependency.
/// </summary>
public sealed class PublishDomainEventsInterceptor : SaveChangesInterceptor
{
    private readonly IPublisher _publisher;

    public PublishDomainEventsInterceptor(IPublisher publisher)
    {
        _publisher = publisher;
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            await PublishDomainEventsAsync(eventData.Context, cancellationToken);
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private async Task PublishDomainEventsAsync(DbContext context, CancellationToken cancellationToken)
    {
        var entitiesWithEvents = context.ChangeTracker
            .Entries<Entity>()
            .Select(entry => entry.Entity)
            .Where(entity => entity.DomainEvents.Count > 0)
            .ToList();

        // Snapshot the events, then clear them, before publishing. Clearing first prevents an
        // infinite loop if a handler triggers another SaveChanges on the same tracked entity.
        var domainEvents = entitiesWithEvents
            .SelectMany(entity => entity.DomainEvents)
            .ToList();

        foreach (var entity in entitiesWithEvents)
        {
            entity.ClearDomainEvents();
        }

        foreach (var domainEvent in domainEvents)
        {
            var notification = CreateNotification(domainEvent);
            await _publisher.Publish(notification, cancellationToken);
        }
    }

    // The concrete DomainEventNotification<T> is built via reflection because the closed generic
    // type is only known at runtime from the event's actual type.
    private static INotification CreateNotification(IDomainEvent domainEvent)
    {
        var notificationType = typeof(DomainEventNotification<>).MakeGenericType(domainEvent.GetType());

        return (INotification)Activator.CreateInstance(notificationType, domainEvent)!;
    }
}
