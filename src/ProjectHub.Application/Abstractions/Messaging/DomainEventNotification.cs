using MediatR;
using ProjectHub.Domain.Abstractions;

namespace ProjectHub.Application.Abstractions.Messaging;

/// <summary>
/// Adapts a Domain <see cref="IDomainEvent"/> to MediatR's <see cref="INotification"/> without
/// forcing the Domain layer to reference MediatR. The Domain stays a pure, dependency-free core;
/// the Application layer owns the transport concern. Handlers subscribe by implementing
/// <c>INotificationHandler&lt;DomainEventNotification&lt;TDomainEvent&gt;&gt;</c>.
/// </summary>
public sealed record DomainEventNotification<TDomainEvent>(TDomainEvent DomainEvent) : INotification
    where TDomainEvent : IDomainEvent;
