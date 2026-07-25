using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectHub.Domain.Entities;
using ProjectHub.Persistence.Constants;

namespace ProjectHub.Persistence.Configurations;

/// <summary>
/// Maps the <see cref="Role"/> aggregate and seeds the three well-known roles the domain exposes
/// as static members, so a fresh database is usable without a manual insert step.
/// </summary>
internal sealed class RoleConfiguration : EntityConfiguration<Role>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles", Schemas.Identity);

        builder.Property(role => role.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(role => role.Name)
            .IsUnique();
    }
}
