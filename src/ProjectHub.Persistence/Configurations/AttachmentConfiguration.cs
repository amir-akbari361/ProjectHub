using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectHub.Domain.Entities;
using ProjectHub.Persistence.Constants;

namespace ProjectHub.Persistence.Configurations;

/// <summary>
/// Maps the <see cref="Attachment"/> aggregate root. The <c>File</c> metadata is a multi-value
/// value object, so its FileName/ContentType/SizeInBytes are persisted inline with OwnsOne.
/// </summary>
internal sealed class AttachmentConfiguration : EntityConfiguration<Attachment>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("attachments", Schemas.Collaboration);

        builder.Property(attachment => attachment.TaskId)
            .IsRequired();

        builder.Property(attachment => attachment.UploadedBy)
            .IsRequired();

        builder.Property(attachment => attachment.StoragePath)
            .HasMaxLength(1000)
            .IsRequired();

        builder.OwnsOne(attachment => attachment.File, file =>
        {
            file.Property(metadata => metadata.FileName)
                .HasColumnName("file_name")
                .HasMaxLength(260)
                .IsRequired();

            file.Property(metadata => metadata.ContentType)
                .HasColumnName("content_type")
                .HasMaxLength(200)
                .IsRequired();

            file.Property(metadata => metadata.SizeInBytes)
                .HasColumnName("size_bytes")
                .IsRequired();
        });

        builder.Navigation(attachment => attachment.File)
            .IsRequired();

        builder.HasOne<ProjectTask>()
            .WithMany()
            .HasForeignKey(attachment => attachment.TaskId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(attachment => attachment.TaskId);
    }
}
