using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectHub.Domain.Entities;
using ProjectHub.Persistence.Constants;

namespace ProjectHub.Persistence.Configurations;

/// <summary>
/// Maps the <see cref="Notification"/> aggregate root. The composite index on
/// (RecipientId, IsRead) backs the hot query "unread notifications for the current user".
/// </summary>
internal sealed class NotificationConfiguration : EntityConfiguration<Notification>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications", Schemas.Collaboration);

        builder.Property(notification => notification.RecipientId)
            .IsRequired();

        builder.Property(notification => notification.Type)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(notification => notification.Message)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(notification => notification.IsRead)
            .IsRequired();

        builder.Property(notification => notification.ReadAtUtc);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(notification => notification.RecipientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(notification => new { notification.RecipientId, notification.IsRead });
    }
}
