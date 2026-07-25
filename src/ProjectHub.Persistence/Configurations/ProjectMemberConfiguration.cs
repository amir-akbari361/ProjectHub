using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectHub.Domain.Entities;
using ProjectHub.Persistence.Constants;

namespace ProjectHub.Persistence.Configurations;

/// <summary>
/// Maps the <see cref="ProjectMember"/> child entity. The FK back to <see cref="Project"/> is
/// declared on the owning side (<see cref="ProjectConfiguration"/>); here we add its own columns
/// and the "one membership per user per project" unique constraint.
/// </summary>
internal sealed class ProjectMemberConfiguration : EntityConfiguration<ProjectMember>
{
    protected override void ConfigureEntity(EntityTypeBuilder<ProjectMember> builder)
    {
        builder.ToTable("project_members", Schemas.Projects);

        builder.Property(member => member.ProjectId)
            .IsRequired();

        builder.Property(member => member.UserId)
            .IsRequired();

        builder.Property(member => member.Role)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(member => new { member.ProjectId, member.UserId })
            .IsUnique();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(member => member.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
