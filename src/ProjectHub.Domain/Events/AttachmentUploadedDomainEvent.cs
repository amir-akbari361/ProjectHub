using ProjectHub.Domain.Abstractions;

namespace ProjectHub.Domain.Events;

public sealed record AttachmentUploadedDomainEvent(
    Guid AttachmentId,
    Guid TaskId,
    Guid UploadedBy,
    string FileName,
    DateTime OccurredAtUtc) : IDomainEvent;
