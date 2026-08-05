namespace ProjectHub.Application.Features.Attachments.DownloadAttachment;

/// <summary>
/// The payload a successful download returns to the API layer. It carries a live <see cref="Content"/>
/// STREAM (not a byte[]) plus the two headers a browser needs — the original <see cref="FileName"/> for the
/// Content-Disposition and the <see cref="ContentType"/> for Content-Type.
/// </summary>
/// <remarks>
/// WHY A STREAM AND NOT byte[]?
/// Same reason the <c>IFileStorage</c> port exposes streams: a 25 MB file buffered as a byte[] is 25 MB of
/// Large Object Heap per concurrent download. Handing the API layer an open stream lets ASP.NET copy from
/// the backing store straight to the response socket in bounded buffers, so memory stays flat.
///
/// WHO DISPOSES THE STREAM?
/// NOT the handler — if the handler disposed it, the bytes would be gone before the controller could copy
/// them. Ownership passes to the API layer, which returns <c>File(stream, ...)</c>; ASP.NET Core disposes
/// the stream once the response has been written. This is why this type is a plain carrier with no
/// using/dispose logic of its own.
///
/// WHY NOT REUSE AttachmentListItemResponse?
/// That DTO is metadata-only for a grid. A download is a fundamentally different shape (it OWNS a stream),
/// so it gets its own result type — conflating them would put a disposable stream on a list row that never
/// needs one.
/// </remarks>
public sealed record DownloadAttachmentResult(
    Stream Content,
    string FileName,
    string ContentType);
