using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectHub.Domain.Entities;
using ProjectHub.Persistence.Constants;

namespace ProjectHub.Persistence.Configurations;

/// <summary>
/// Maps the <see cref="User"/> aggregate root: its <c>Email</c> value object, scalar profile
/// columns, and the owned <see cref="UserRole"/> join collection reached only through the root.
/// </summary>
internal sealed class UserConfiguration : EntityConfiguration<User>
{
    protected override void ConfigureEntity(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", Schemas.Identity);

        // Email is a single-value ValueObject. Instead of a separate table we flatten it into one
        // column and convert to/from the primitive string. HasConversion keeps the domain type in
        // C# while storing a plain nvarchar in SQL.
        builder.Property(user => user.Email)
            .HasConversion(email => email.Value, value => ProjectHub.Domain.ValueObjects.Email.Create(value))
            .HasColumnName("email")
            .HasMaxLength(320)
            .IsRequired();

        // A unique index enforces "one account per email" at the database level — the ultimate
        // guard even if application-level checks race under concurrency.
        builder.HasIndex(user => user.Email)
            .IsUnique();

        builder.Property(user => user.FirstName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(user => user.LastName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(user => user.PasswordHash)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(user => user.IsEmailConfirmed)
            .IsRequired();

        builder.Property(user => user.IsActive)
            .IsRequired();

        // FullName is a computed C# property, never a stored column — tell EF to ignore it.
        builder.Ignore(user => user.FullName);

        // The roles collection is exposed as read-only; EF writes through the private backing field.
        var rolesNavigation = builder.Metadata.FindNavigation(nameof(User.Roles))!;
        rolesNavigation.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(user => user.Roles)
            .WithOne()
            .HasForeignKey(userRole => userRole.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
