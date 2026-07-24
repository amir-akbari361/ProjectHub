using ProjectHub.Domain.Entities;
using ProjectHub.Domain.Enums;
using ProjectHub.Domain.Events;
using ProjectHub.Domain.Exceptions;

namespace ProjectHub.Domain.Tests.Entities;

public class NotificationTests
{
    private static readonly DateTime UtcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid RecipientId = Guid.NewGuid();

    private static Notification CreateNotification() =>
        Notification.Create(RecipientId, NotificationType.TaskAssigned, "You were assigned a task", UtcNow);

    [Fact]
    public void Create_ShouldReturnUnreadNotification_AndRaiseEvent()
    {
        var notification = CreateNotification();

        Assert.NotEqual(Guid.Empty, notification.Id);
        Assert.Equal(RecipientId, notification.RecipientId);
        Assert.Equal(NotificationType.TaskAssigned, notification.Type);
        Assert.False(notification.IsRead);
        Assert.Null(notification.ReadAtUtc);
        Assert.Contains(notification.DomainEvents, e => e is NotificationCreatedDomainEvent);
    }

    [Fact]
    public void Create_ShouldThrow_WhenMessageIsEmpty()
    {
        Assert.Throws<ArgumentException>(() =>
            Notification.Create(RecipientId, NotificationType.CommentAdded, "  ", UtcNow));
    }

    [Fact]
    public void Create_ShouldThrow_WhenMessageTooLong()
    {
        var tooLong = new string('x', 501);

        Assert.Throws<DomainException>(() =>
            Notification.Create(RecipientId, NotificationType.CommentAdded, tooLong, UtcNow));
    }

    [Fact]
    public void MarkAsRead_ShouldSetReadState_AndRaiseEvent()
    {
        var notification = CreateNotification();
        notification.ClearDomainEvents();
        var readTime = UtcNow.AddMinutes(5);

        notification.MarkAsRead(readTime);

        Assert.True(notification.IsRead);
        Assert.Equal(readTime, notification.ReadAtUtc);
        Assert.Contains(notification.DomainEvents, e => e is NotificationReadDomainEvent);
    }

    [Fact]
    public void MarkAsRead_ShouldBeIdempotent_WhenAlreadyRead()
    {
        var notification = CreateNotification();
        notification.MarkAsRead(UtcNow);
        notification.ClearDomainEvents();

        notification.MarkAsRead(UtcNow.AddHours(1));

        Assert.Equal(UtcNow, notification.ReadAtUtc);
        Assert.Empty(notification.DomainEvents);
    }
}
