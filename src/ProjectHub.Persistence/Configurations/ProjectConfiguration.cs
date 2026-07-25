using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectHub.Domain.Entities;
using ProjectHub.Domain.ValueObjects;
using ProjectHub.Persistence.Constants;

namespace ProjectHub.Persistence.Configurations;

/// <summary>
/// Maps the <see cref="Project"/> aggregate root and its owned <see cref="ProjectMember"/>
/// collection. Members exist only inside a project, so they are reached through the root's
/// navigation and share its lifecycle.
/// </summary>
internal sealed class ProjectConfiguration : EntityConfiguration<Project>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("projects", Schemas.Projects);

        builder.Property(project => project.Name)
            .HasConversion(name => name.Value, value => ProjectName.Create(value))
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(project => project.Description)
            .HasMaxLength(2000);

        // Store the enum as its string name, not its int. Readable in raw SQL and safe against
        // accidental reordering of enum members changing the stored meaning.
        builder.Property(project => project.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        var membersNavigation = builder.Metadata.FindNavigation(nameof(Project.Members))!;
        membersNavigation.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(project => project.Members)
            .WithOne()
            .HasForeignKey(member => member.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
