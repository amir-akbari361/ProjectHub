using ProjectHub.Domain.Enums;

namespace ProjectHub.Application.Features.Notifications.EventHandlers;

/// <summary>
/// A single "notify this user with this message" instruction produced by a notification event handler.
/// Pairing the recipient with the type and the already-composed text lets <see cref="NotificationEventHandler{T}"/>
/// treat every notification identically — exclude the actor, de-duplicate, create, persist — regardless of
/// which domain event produced it. A handler returns zero, one, or many of these per event.
/// </summary>
public sealed record NotificationRequest(Guid RecipientId, NotificationType Type, string Message);
