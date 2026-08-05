using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Application.Abstractions.Storage;
using ProjectHub.Application.Common;

namespace ProjectHub.Application.Features.Attachments.DownloadAttachment;

/// <summary>
/// Handles <see cref="DownloadAttachmentQuery"/>. A READ-side handler that verifies the caller can see the
/// attachment (via task -> project -> membership), fetches ONLY the three fields a download needs
/// (storage key + the two response headers) as a SQL projection, then opens the byte stream through the
/// <see cref="IFileStorage"/> port and hands it back for the API layer to stream to the client.
/// </summary>
/// <remarks>
/// WHY IS ANY MEMBER ALLOWED TO DOWNLOAD (EVEN A VIEWER)?
/// An attachment is reference material for the whole team. Reading it requires only that you can see the
/// project — the lowest role (Viewer) qualifies. Contrast with Delete, which needs uploader-or-Maintainer.
/// So the authorization here is pure VISIBILITY, identical to ListAttachments; no role comparison at all.
///
/// WHY PROJECT INSTEAD OF LOADING THE AGGREGATE?
/// We need three scalar values, not behavior. A projection of (StoragePath, FileName, ContentType) is one
/// lean SELECT with no tracking overhead, and it keeps StoragePath server-side — it is read here purely to
/// hand to the storage port and is NEVER returned to the client.
///
/// WHY OPEN THE STREAM IN THE HANDLER BUT NOT DISPOSE IT?
/// Opening belongs to the Application layer (it owns the port). Disposing does NOT — the stream must stay
/// live until the controller has copied it to the response. Ownership therefore transfers to the API layer
/// via <see cref="DownloadAttachmentResult"/>; ASP.NET disposes it after writing. If we wrapped it in a
/// using here the bytes would be gone before the client received them.
///
/// WHAT IF THE ROW EXISTS BUT THE BYTES ARE MISSING?
/// That is an integrity fault (a row without its blob), not a business outcome. <c>OpenReadAsync</c> throws;
/// we let it bubble to the global exception handler as a 500, because a modeled 404 would hide real
/// corruption behind an "expected" response.
/// </remarks>
public sealed class DownloadAttachmentQueryHandler
    : IQueryHandler<DownloadAttachmentQuery, DownloadAttachmentResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IFileStorage _fileStorage;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<DownloadAttachmentQueryHandler> _logger;

    public DownloadAttachmentQueryHandler(
        IApplicationDbContext context,
        IFileStorage fileStorage,
        ICurrentUser currentUser,
        ILogger<DownloadAttachmentQueryHandler> logger)
    {
        _context = context;
        _fileStorage = fileStorage;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<DownloadAttachmentResult>> Handle(
        DownloadAttachmentQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Resolve the caller. Downloads are scoped to project membership, so an unauthenticated request
        //    has nothing it could legitimately fetch — fail fast with 401 before touching the DB.
        if (_currentUser.UserId is not { } userId)
        {
            _logger.LogWarning("DownloadAttachment reached the handler without an authenticated user.");
            return Result.Failure<DownloadAttachmentResult>(Error.Unauthorized(
                "Attachments.Unauthenticated",
                "You must be signed in to download an attachment."));
        }

        // 2. In ONE query: confirm the attachment is visible to this caller AND grab the three fields we
        //    need. The membership check is baked into the WHERE, so a non-member gets no row — indistinguishable
        //    from "does not exist". AsNoTracking() — pure read, no aggregate materialized.
        var metadata = await _context.Attachments
            .AsNoTracking()
            .Where(a => a.Id == request.AttachmentId)
            .Where(a => _context.ProjectTasks.Any(
                t => t.Id == a.TaskId
                     && _context.Projects.Any(
                         p => p.Id == t.ProjectId && p.Members.Any(m => m.UserId == userId))))
            .Select(a => new
            {
                a.StoragePath,
                a.File.FileName,
                a.File.ContentType
            })
            .SingleOrDefaultAsync(cancellationToken);

        // Unknown id OR not a member both collapse to the SAME NotFound (no existence disclosure).
        if (metadata is null)
        {
            _logger.LogInformation(
                "DownloadAttachment: attachment {AttachmentId} not found or not visible to user {UserId}.",
                request.AttachmentId, userId);
            return Result.Failure<DownloadAttachmentResult>(
                AttachmentErrors.NotFound(request.AttachmentId));
        }

        // 3. Open the byte stream through the port. NOT disposed here — ownership passes to the API layer
        //    (see the remarks). A missing blob throws and surfaces as a 500 integrity error, by design.
        var content = await _fileStorage.OpenReadAsync(metadata.StoragePath, cancellationToken);

        _logger.LogInformation(
            "Attachment {AttachmentId} opened for download by user {UserId}.",
            request.AttachmentId, userId);

        return new DownloadAttachmentResult(content, metadata.FileName, metadata.ContentType);
    }
}
