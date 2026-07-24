using ProjectHub.Domain.Entities;
using ProjectHub.Domain.Events;
using ProjectHub.Domain.Exceptions;
using ProjectHub.Domain.ValueObjects;

namespace ProjectHub.Domain.Tests.Entities;

public class AttachmentTests
{
    private static readonly DateTime UtcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid TaskId = Guid.NewGuid();
    private static readonly Guid UploaderId = Guid.NewGuid();

    private static FileMetadata ValidFile() =>
        FileMetadata.Create("spec.pdf", "application/pdf", 1024);

    [Fact]
    public void Upload_ShouldCreateAttachment_AndRaiseEvent()
    {
        var attachment = Attachment.Upload(TaskId, UploaderId, ValidFile(), "/blob/spec.pdf", UtcNow);

        Assert.NotEqual(Guid.Empty, attachment.Id);
        Assert.Equal(TaskId, attachment.TaskId);
        Assert.Equal(UploaderId, attachment.UploadedBy);
        Assert.Equal("spec.pdf", attachment.File.FileName);
        Assert.Equal("/blob/spec.pdf", attachment.StoragePath);
        Assert.Contains(attachment.DomainEvents, e => e is AttachmentUploadedDomainEvent);
    }

    [Fact]
    public void Upload_ShouldThrow_WhenStoragePathIsEmpty()
    {
        Assert.Throws<ArgumentException>(() =>
            Attachment.Upload(TaskId, UploaderId, ValidFile(), " ", UtcNow));
    }

    [Fact]
    public void FileMetadata_ShouldThrow_WhenSizeIsZeroOrNegative()
    {
        Assert.Throws<DomainException>(() => FileMetadata.Create("a.txt", "text/plain", 0));
        Assert.Throws<DomainException>(() => FileMetadata.Create("a.txt", "text/plain", -5));
    }

    [Fact]
    public void FileMetadata_ShouldThrow_WhenSizeExceedsLimit()
    {
        var tooBig = (25L * 1024 * 1024) + 1;

        Assert.Throws<DomainException>(() => FileMetadata.Create("big.zip", "application/zip", tooBig));
    }

    [Fact]
    public void FileMetadata_Equality_ShouldBeByValue()
    {
        var a = FileMetadata.Create("a.txt", "text/plain", 100);
        var b = FileMetadata.Create("a.txt", "text/plain", 100);
        var c = FileMetadata.Create("a.txt", "text/plain", 200);

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }
}
