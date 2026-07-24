using ProjectHub.Domain.Common;
using ProjectHub.Domain.Enums;
using ProjectHub.Domain.Events;
using ProjectHub.Domain.Exceptions;
using ProjectHub.Domain.Primitives;

namespace ProjectHub.Domain.Entities;

public sealed class Notification : AggregateRoot
{
    private const int MaxMessageLength = 500;

    private Notification(Guid id, Guid recipientId, NotificationType type, string message)
        : base(id)
    {
        RecipientId = recipientId;
        Type = type;
        Message = message;
    }

    private Notification()
        : base(Guid.Empty)
    {
        Message = null!;
    }

    public Guid RecipientId { get; private set; }

    public NotificationType Type { get; private set; }

    public string Message { get; private set; }

    public bool IsRead { get; private set; }

    public DateTime? ReadAtUtc { get; private set; }

    public static Notification Create(
        Guid recipientId,
        NotificationType type,
        string message,
        DateTime utcNow)
    {
        Guard.NotEmpty(recipientId, nameof(recipientId));
        var normalized = Guard.NotNullOrWhiteSpace(message, nameof(message)).Trim();

        if (normalized.Length > MaxMessageLength)
        {
            throw new DomainException($"Notification message cannot exceed {MaxMessageLength} characters.");
        }

        var notification = new Notification(Guid.NewGuid(), recipientId, type, normalized);
        notification.MarkCreated(utcNow);
        notification.RaiseDomainEvent(
            new NotificationCreatedDomainEvent(notification.Id, recipientId, type, utcNow));

        return notification;
    }

    public void MarkAsRead(DateTime utcNow)
    {
        if (IsRead)
        {
            return;
        }

        IsRead = true;
        ReadAtUtc = utcNow;
        MarkUpdated(utcNow);
        RaiseDomainEvent(new NotificationReadDomainEvent(Id, RecipientId, utcNow));
    }
}
