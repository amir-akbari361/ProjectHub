using ProjectHub.Application.Abstractions.Messaging;

namespace ProjectHub.Application.Features.Attachments.UploadAttachment;

/// <summary>
/// Command to attach an uploaded file to a task. Carries the parent task id (from the route), the file's
/// declared metadata (name, content type, size), and — crucially — a factory that yields the byte STREAM
/// on demand rather than the bytes themselves.
/// </summary>
/// <remarks>
/// WHY A <c>Func&lt;Stream&gt;</c> AND NOT A <c>Stream</c> OR <c>byte[]</c>?
/// A command flows through the whole MediatR pipeline (logging, validation, performance behaviors) before
/// it reaches the handler. If we put a live <c>Stream</c> on it, a behavior could accidentally consume or
/// dispose it, and the handler would receive an empty/closed stream. A <c>byte[]</c> would force us to
/// buffer the entire (up to 25 MB) file in memory just to pass it around. A deferred stream FACTORY sidesteps
/// both: the pipeline sees an opaque delegate, and ONLY the handler opens the stream, exactly once, at the
/// moment it copies to storage. The controller owns opening it from the request body.
///
/// WHY IS THE UPLOADER NOT ON THE COMMAND?
/// Attribution comes from the authenticated principal (<c>ICurrentUser</c>) in the handler, never from the
/// client — the same rule as comments. A client cannot upload "as" someone else.
/// </remarks>
public sealed record UploadAttachmentCommand(
    Guid TaskId,
    string FileName,
    string ContentType,
    long SizeInBytes,
    Func<Stream> OpenReadStream) : ICommand<UploadAttachmentResponse>;
