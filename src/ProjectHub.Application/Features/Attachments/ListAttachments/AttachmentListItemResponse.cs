namespace ProjectHub.Application.Features.Attachments.ListAttachments;

/// <summary>
/// A single row in a task's attachment list. A READ-side DTO projected directly from the database — it
/// carries only the metadata a client needs to render the list and build a download link, NEVER the bytes
/// or the internal <c>StoragePath</c> (which is an implementation detail of the storage adapter and would
/// leak the physical layout if exposed). To fetch the actual file the client calls the download endpoint
/// with <see cref="Id"/>.
/// </summary>
public sealed record AttachmentListItemResponse(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeInBytes,
    Guid UploadedBy,
    DateTime UploadedAtUtc);
