using ProjectHub.Domain.Common;
using ProjectHub.Domain.Events;
using ProjectHub.Domain.Primitives;
using ProjectHub.Domain.ValueObjects;

namespace ProjectHub.Domain.Entities;

public sealed class Attachment : AggregateRoot
{
    private Attachment(Guid id, Guid taskId, Guid uploadedBy, FileMetadata file, string storagePath)
        : base(id)
    {
        TaskId = taskId;
        UploadedBy = uploadedBy;
        File = file;
        StoragePath = storagePath;
    }

    private Attachment()
        : base(Guid.Empty)
    {
        File = null!;
        StoragePath = null!;
    }

    public Guid TaskId { get; private set; }

    public Guid UploadedBy { get; private set; }

    public FileMetadata File { get; private set; }

    public string StoragePath { get; private set; }

    public static Attachment Upload(
        Guid taskId,
        Guid uploadedBy,
        FileMetadata file,
        string storagePath,
        DateTime utcNow)
    {
        Guard.NotEmpty(taskId, nameof(taskId));
        Guard.NotEmpty(uploadedBy, nameof(uploadedBy));
        Guard.NotNull(file, nameof(file));
        var normalizedPath = Guard.NotNullOrWhiteSpace(storagePath, nameof(storagePath)).Trim();

        var attachment = new Attachment(Guid.NewGuid(), taskId, uploadedBy, file, normalizedPath);
        attachment.MarkCreated(utcNow, uploadedBy);
        attachment.RaiseDomainEvent(
            new AttachmentUploadedDomainEvent(attachment.Id, taskId, uploadedBy, file.FileName, utcNow));

        return attachment;
    }
}
