using ProjectHub.Application.Abstractions.Messaging;

namespace ProjectHub.Application.Features.Attachments.DownloadAttachment;

/// <summary>
/// Retrieves the BYTES of a single attachment for download. A READ-side query keyed only by the attachment
/// id — the caller is resolved server-side from the JWT for the visibility check, never trusted from the
/// request.
/// </summary>
public sealed record DownloadAttachmentQuery(Guid AttachmentId)
    : IQuery<DownloadAttachmentResult>;
