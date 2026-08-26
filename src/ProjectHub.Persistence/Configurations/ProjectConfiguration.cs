using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectHub.Domain.Entities;
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

        // WHY ComplexProperty AND NOT HasConversion?
        // A value converter makes the value object an OPAQUE SCALAR to the query pipeline: EF can compare it for
        // equality and nothing more. Any expression that reaches INSIDE it — `p.Name.Value` in a LIKE or an
        // ORDER BY — cannot be translated, and EF throws at query time. That silently broke five endpoints
        // (project search, project sort-by-name, task search, task sort-by-title, and global search) with a 500;
        // the failure surfaces only when a caller actually supplies a search term or that sort key, which is why
        // it was not obvious from a smoke test of the happy path.
        //
        // Mapping it as a COMPLEX PROPERTY instead makes `Value` a first-class mapped column, so every one of
        // those expressions translates to plain SQL and the Application layer needs no per-query workaround
        // (no EF.Property<string>, no shadow properties). The column name, length and nullability are unchanged,
        // so the database schema is identical and no migration is required — this is purely a change in how EF
        // understands the shape it already stores.
        builder.ComplexProperty(project => project.Name, name =>
        {
            name.Property(projectName => projectName.Value)
                .HasColumnName("name")
                .HasMaxLength(200)
                .IsRequired();
        });

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
