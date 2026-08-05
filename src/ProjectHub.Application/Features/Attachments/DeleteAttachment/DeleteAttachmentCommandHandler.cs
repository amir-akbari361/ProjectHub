using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Application.Abstractions.Storage;
using ProjectHub.Application.Common;
using ProjectHub.Domain.Entities;
using ProjectHub.Domain.Enums;

namespace ProjectHub.Application.Features.Attachments.DeleteAttachment;

/// <summary>
/// Handles <see cref="DeleteAttachmentCommand"/>. A WRITE-side handler that loads the attachment, authorizes
/// the caller (the original uploader OR a project Maintainer/Owner), soft-deletes the metadata row and commits, then

/// deletes the underlying bytes.
/// </summary>
/// <remarks>
/// THE DUAL-WRITE PROBLEM, MIRRORED FROM UPLOAD — BUT WITH THE OPPOSITE ORDERING:
/// Upload wrote bytes FIRST so a DB failure left an invisible orphan (safe) rather than a broken row
/// (visible). Delete reverses the priority. We commit the DB soft-delete FIRST, and only then delete the
/// bytes. Why?
///   • If we deleted bytes first and the DB commit then failed, we'd have a row that says "this file exists"
///     pointing at bytes that are GONE — a broken download the user can see. Worst outcome.
///   • By committing the row-removal first, the worst residual failure is an orphaned blob (row gone, bytes
///     linger) — invisible and reclaimable by the same background sweeper Upload relies on.
/// So both handlers follow ONE principle: never leave a VISIBLE-but-BROKEN state; prefer an invisible,
/// reclaimable orphan. The safe ordering just flips depending on which store holds the user-facing truth.
///
/// WHY SOFT-DELETE THE ROW BUT HARD-DELETE THE BYTES?
/// The metadata row participates in our global soft-delete convention (audit trail, "who deleted what
/// when"). The bytes carry no audit value and cost real storage, so once the row is gone we reclaim them
/// for good. The blob delete is idempotent, so a retry after a partial failure is safe.
///
/// WHO MAY DELETE?
/// The uploader (you can remove what you added) OR a project Maintainer/Owner (housekeeping authority). A
/// plain Contributor cannot delete someone else's file — that would let peers destroy each other's evidence.

/// </remarks>
public sealed class DeleteAttachmentCommandHandler : ICommandHandler<DeleteAttachmentCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IRepository<Attachment> _attachments;
    private readonly IFileStorage _fileStorage;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeleteAttachmentCommandHandler> _logger;

    public DeleteAttachmentCommandHandler(
        IApplicationDbContext context,
        IRepository<Attachment> attachments,
        IFileStorage fileStorage,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        ILogger<DeleteAttachmentCommandHandler> logger)
    {
        _context = context;
        _attachments = attachments;
        _fileStorage = fileStorage;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> Handle(DeleteAttachmentCommand request, CancellationToken cancellationToken)
    {
        // 1. Resolve the caller. Deleting is attributed and authorized, so no principal => 401.
        if (_currentUser.UserId is not { } userId)
        {
            _logger.LogWarning("DeleteAttachment reached the handler without an authenticated user.");
            return Result.Failure(Error.Unauthorized(
                "Attachments.Unauthenticated",
                "You must be signed in to delete an attachment."));
        }

        // 2. Load the aggregate we intend to mutate. TRACKED (no AsNoTracking) — we are about to remove it,
        //    so EF must watch it. A missing row => NotFound (also covers already-soft-deleted via the filter).
        var attachment = await _attachments.GetByIdAsync(request.AttachmentId, cancellationToken);

        if (attachment is null)
        {
            _logger.LogInformation(
                "DeleteAttachment: attachment {AttachmentId} not found for user {UserId}.",
                request.AttachmentId, userId);
            return Result.Failure(AttachmentErrors.NotFound(request.AttachmentId));
        }

        // 3. Resolve the caller's role in the attachment's project (via its task) to decide if a NON-uploader
        //    is nonetheless a Manager. A read-only projection of just the role; null => not a member.
        var callerRole = await _context.ProjectTasks
            .AsNoTracking()
            .Where(t => t.Id == attachment.TaskId)
            .SelectMany(t => _context.Projects
                .Where(p => p.Id == t.ProjectId)
                .SelectMany(p => p.Members)
                .Where(m => m.UserId == userId)
                .Select(m => (ProjectRole?)m.Role))
            .SingleOrDefaultAsync(cancellationToken);

        // A non-member cannot even see the attachment: collapse to the SAME NotFound (no disclosure).
        if (callerRole is null)
        {
            _logger.LogInformation(
                "DeleteAttachment: user {UserId} is not a member of the project owning attachment {AttachmentId}.",
                userId, request.AttachmentId);
            return Result.Failure(AttachmentErrors.NotFound(request.AttachmentId));
        }

        // 4. Authorize: the uploader OR a Maintainer/Owner may delete. Anyone else is a member who lacks the
        //    right => 403. Maintainer is the lowest role trusted with housekeeping over other people's files.
        var isUploader = attachment.UploadedBy == userId;
        var isManager = callerRole >= ProjectRole.Maintainer;

        if (!isUploader && !isManager)
        {
            _logger.LogInformation(
                "DeleteAttachment: user {UserId} (role {Role}) may not delete attachment {AttachmentId} owned by {Owner}.",
                userId, callerRole, request.AttachmentId, attachment.UploadedBy);
            return Result.Failure(AttachmentErrors.Forbidden);
        }


        // 5. Capture the storage key BEFORE removing the row — after SaveChanges the tracked entity's state
        //    is spent, and we still need the key to reclaim the bytes.
        var storagePath = attachment.StoragePath;

        // 6. Soft-delete the metadata row and commit FIRST (see the ordering rationale in the remarks).
        _attachments.Remove(attachment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 7. Now reclaim the bytes. This is best-effort: the user-facing truth (the row) is already gone, so a
        //    blob-delete failure only leaves an invisible orphan for the sweeper — never a broken download.
        try
        {
            await _fileStorage.DeleteAsync(storagePath, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "DeleteAttachment: row {AttachmentId} removed but blob {StoragePath} could not be deleted; it is now an orphan.",
                request.AttachmentId, storagePath);
        }

        _logger.LogInformation(
            "Attachment {AttachmentId} deleted by user {UserId}.", request.AttachmentId, userId);

        return Result.Success();
    }
}
