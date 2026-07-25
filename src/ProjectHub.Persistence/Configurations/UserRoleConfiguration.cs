using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectHub.Domain.Entities;
using ProjectHub.Persistence.Constants;

namespace ProjectHub.Persistence.Configurations;

/// <summary>
/// Maps the <see cref="UserRole"/> join entity that links a <see cref="User"/> to a
/// <see cref="Role"/>. It is not an aggregate root — it is created only through the User aggregate —
/// but it still carries the audit/soft-delete columns from <see cref="Entity"/>.
/// </summary>
internal sealed class UserRoleConfiguration : EntityConfiguration<UserRole>
{
    protected override void ConfigureEntity(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("user_roles", Schemas.Identity);

        builder.Property(userRole => userRole.UserId)
            .IsRequired();

        builder.Property(userRole => userRole.RoleId)
            .IsRequired();

        // A user must not hold the same role twice; enforce the pair uniqueness in the database.
        builder.HasIndex(userRole => new { userRole.UserId, userRole.RoleId })
            .IsUnique();

        // Point the FK at Role without a reverse navigation (Role does not know its members).
        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(userRole => userRole.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
