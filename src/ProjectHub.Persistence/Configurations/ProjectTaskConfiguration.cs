using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectHub.Domain.Entities;
using ProjectHub.Domain.ValueObjects;
using ProjectHub.Persistence.Constants;

namespace ProjectHub.Persistence.Configurations;

/// <summary>
/// Maps the <see cref="ProjectTask"/> aggregate root. Its <c>Title</c> value object is flattened to
/// a single column, and both status and priority enums are persisted as readable strings. Indexes
/// on ProjectId, AssigneeId, and Status back the board's most common query paths.
/// </summary>
internal sealed class ProjectTaskConfiguration : EntityConfiguration<ProjectTask>
{
    protected override void ConfigureEntity(EntityTypeBuilder<ProjectTask> builder)
    {
        builder.ToTable("tasks", Schemas.Projects);

        builder.Property(task => task.ProjectId)
            .IsRequired();

        builder.Property(task => task.Title)
            .HasConversion(title => title.Value, value => TaskTitle.Create(value))
            .HasColumnName("title")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(task => task.Description)
            .HasMaxLength(4000);

        builder.Property(task => task.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(task => task.Priority)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(task => task.AssigneeId);

        builder.Property(task => task.DueDate);

        // Every task belongs to a project; deleting a project soft-deletes its tasks with it, so
        // Restrict is safe (soft delete never triggers a hard cascade).
        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(task => task.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(task => task.AssigneeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(task => task.ProjectId);
        builder.HasIndex(task => task.AssigneeId);
        builder.HasIndex(task => new { task.ProjectId, task.Status });
    }
}
