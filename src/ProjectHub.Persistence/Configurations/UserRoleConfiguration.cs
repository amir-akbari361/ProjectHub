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

        // A user must not hold the same role twice — but only LIVE rows count.
        //
        // WHY THE FILTER IS LOAD-BEARING, NOT AN OPTIMISATION
        // Revoking a role soft-deletes the join row (the base configuration's policy: rows are never
        // physically removed). An UNFILTERED unique index keeps counting that tombstone, so granting a role
        // that had ever been revoked violated the index — the admin console's grant → revoke → grant-again
        // sequence failed with a 500 rather than a 204. Restricting the constraint to IsDeleted = 0 makes the
        // database enforce what the application actually means: at most one ACTIVE assignment per pair, with
        // the revoked rows left behind as the history the soft-delete policy exists to keep.
        builder.HasIndex(userRole => new { userRole.UserId, userRole.RoleId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        // Point the FK at Role. The navigation lives on UserRole so a loaded user can read its role
        // NAMES; Role still has no reverse navigation (it does not track its members). This maps onto the
        // existing RoleId column — no schema change.
        builder.HasOne(userRole => userRole.Role)
            .WithMany()
            .HasForeignKey(userRole => userRole.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
