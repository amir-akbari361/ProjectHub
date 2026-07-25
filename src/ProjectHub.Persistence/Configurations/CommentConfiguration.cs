using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectHub.Domain.Entities;
using ProjectHub.Domain.ValueObjects;
using ProjectHub.Persistence.Constants;

namespace ProjectHub.Persistence.Configurations;

/// <summary>
/// Maps the <see cref="Comment"/> aggregate root. Its <c>Body</c> value object is flattened to a
/// single text column; the TaskId index backs the "load a task's comment thread" query.
/// </summary>
internal sealed class CommentConfiguration : EntityConfiguration<Comment>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Comment> builder)
    {
        builder.ToTable("comments", Schemas.Collaboration);

        builder.Property(comment => comment.TaskId)
            .IsRequired();

        builder.Property(comment => comment.AuthorId)
            .IsRequired();

        builder.Property(comment => comment.Body)
            .HasConversion(body => body.Value, value => CommentBody.Create(value))
            .HasColumnName("body")
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(comment => comment.IsEdited)
            .IsRequired();

        builder.HasOne<ProjectTask>()
            .WithMany()
            .HasForeignKey(comment => comment.TaskId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(comment => comment.TaskId);
    }
}
