using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectHub.Domain.Entities;
using ProjectHub.Persistence.Constants;

namespace ProjectHub.Persistence.Configurations;

/// <summary>
/// Maps the <see cref="Sprint"/> aggregate root. The <c>Schedule</c> is a multi-value
/// <c>DateRange</c> value object, so it is mapped with OwnsOne — EF flattens Start/End into two
/// columns on the same table while the domain keeps the rich type.
/// </summary>
internal sealed class SprintConfiguration : EntityConfiguration<Sprint>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Sprint> builder)
    {
        builder.ToTable("sprints", Schemas.Projects);

        builder.Property(sprint => sprint.ProjectId)
            .IsRequired();

        builder.Property(sprint => sprint.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(sprint => sprint.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        // DateRange has no primitive value to convert to; OwnsOne persists its two DateTime
        // components inline. IsRequired keeps the whole owned value non-null.
        builder.OwnsOne(sprint => sprint.Schedule, schedule =>
        {
            schedule.Property(range => range.Start)
                .HasColumnName("schedule_start")
                .IsRequired();

            schedule.Property(range => range.End)
                .HasColumnName("schedule_end")
                .IsRequired();
        });

        builder.Navigation(sprint => sprint.Schedule)
            .IsRequired();

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(sprint => sprint.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(sprint => sprint.ProjectId);
    }
}
