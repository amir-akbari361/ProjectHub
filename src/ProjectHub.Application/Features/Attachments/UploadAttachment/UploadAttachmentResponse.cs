namespace ProjectHub.Application.Features.Attachments.UploadAttachment;

/// <summary>
/// Lean write-side acknowledgement returned after a file is attached: the new attachment's id, its
/// original file name (echoed so the client can render it without re-reading the upload), and the
/// server-stamped upload time. As with comments we do NOT return the whole aggregate or the bytes — the
/// read model and the download stream belong to the query side.
/// </summary>
public sealed record UploadAttachmentResponse(
    Guid Id,
    string FileName,
    DateTime UploadedAtUtc);
