using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Application.Abstractions.Storage;
using ProjectHub.Application.Common;
using ProjectHub.Domain.Entities;
using ProjectHub.Domain.Enums;
using ProjectHub.Domain.Exceptions;
using ProjectHub.Domain.ValueObjects;

namespace ProjectHub.Application.Features.Attachments.UploadAttachment;

/// <summary>
/// Handles <see cref="UploadAttachmentCommand"/>. A WRITE-side handler that authorizes the caller against
/// the parent task's project, streams the bytes into blob storage via <see cref="IFileStorage"/>, records
/// the metadata as an <c>Attachment</c> aggregate in SQL, and commits once through the unit of work.
/// </summary>
/// <remarks>
/// THE DUAL-WRITE PROBLEM (the reason this handler is more than a copy of AddComment):
/// We persist to TWO systems that do NOT share a transaction — the blob store (bytes) and SQL (metadata).
/// There is no distributed transaction spanning both, so a naive "save bytes, then SaveChanges" leaves a
/// window: if the DB commit fails AFTER the bytes landed, we leak an ORPHANED BLOB — bytes on disk that no
/// DB row references, invisible to the app and impossible to clean up through it.
///
/// OUR STRATEGY — order + compensation:
///   1. Save the bytes FIRST and get the storage key. (If this fails, nothing was written to either store —
///      the safest failure, no cleanup needed.)
///   2. Try to commit the metadata row.
///   3. If the commit throws, run a COMPENSATING DELETE of the just-written blob, then rethrow. The blob
///      delete is idempotent, so even if compensation partially ran before, it is safe.
/// The residual risk is the compensation itself failing (process crash between save and delete). In a
/// hardened system you would sweep such orphans with a periodic background job that deletes blobs older
/// than N minutes with no matching row — but the ordering above makes that a rare, recoverable case rather
/// than the norm. We deliberately do NOT save metadata first: a DB row pointing at bytes that were never
/// written is a BROKEN attachment the user can see, which is worse than an invisible orphan.
///
/// WHY OPEN THE STREAM HERE AND NOT ON THE COMMAND?
/// The command carries a deferred <c>Func&lt;Stream&gt;</c> so the pipeline never touches the body. We open it
/// exactly once, inside a using, so it is disposed regardless of outcome.
/// </remarks>
public sealed class UploadAttachmentCommandHandler
    : ICommandHandler<UploadAttachmentCommand, UploadAttachmentResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IRepository<Attachment> _attachments;
    private readonly IFileStorage _fileStorage;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UploadAttachmentCommandHandler> _logger;

    public UploadAttachmentCommandHandler(
        IApplicationDbContext context,
        IRepository<Attachment> attachments,
        IFileStorage fileStorage,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork,
        ILogger<UploadAttachmentCommandHandler> logger)
    {
        _context = context;
        _attachments = attachments;
        _fileStorage = fileStorage;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<UploadAttachmentResponse>> Handle(
        UploadAttachmentCommand request, CancellationToken cancellationToken)
    {
        // 1. Resolve the caller. Uploading is attributed and authorized, so no principal => 401.
        if (_currentUser.UserId is not { } userId)
        {
            _logger.LogWarning("UploadAttachment reached the handler without an authenticated user.");
            return Result.Failure<UploadAttachmentResponse>(Error.Unauthorized(
                "Attachments.Unauthenticated",
                "You must be signed in to upload an attachment."));
        }

        // 2. Resolve the parent task's project. A null projectId means the task is unknown (or
        //    soft-deleted); collapse into a NotFound so we never disclose task existence.
        var projectId = await _context.ProjectTasks
            .AsNoTracking()
            .Where(t => t.Id == request.TaskId)
            .Select(t => (Guid?)t.ProjectId)
            .SingleOrDefaultAsync(cancellationToken);

        if (projectId is null)
        {
            _logger.LogInformation(
                "UploadAttachment: task {TaskId} not found for user {UserId}.", request.TaskId, userId);
            return Result.Failure<UploadAttachmentResponse>(AttachmentErrors.TaskNotFound(request.TaskId));
        }

        // 3. Authorize against the project's membership. A non-member gets the SAME NotFound as an unknown
        //    task (no disclosure); a Viewer is a member but may not upload, so gets 403.
        var callerRole = await _context.Projects
            .AsNoTracking()
            .Where(p => p.Id == projectId)
            .SelectMany(p => p.Members)
            .Where(m => m.UserId == userId)
            .Select(m => (ProjectRole?)m.Role)
            .SingleOrDefaultAsync(cancellationToken);

        if (callerRole is null)
        {
            _logger.LogInformation(
                "UploadAttachment: user {UserId} is not a member of project {ProjectId}.", userId, projectId);
            return Result.Failure<UploadAttachmentResponse>(AttachmentErrors.TaskNotFound(request.TaskId));
        }

        if (callerRole < ProjectRole.Contributor)
        {
            _logger.LogInformation(
                "UploadAttachment: user {UserId} with role {Role} may not upload to project {ProjectId}.",
                userId, callerRole, projectId);
            return Result.Failure<UploadAttachmentResponse>(AttachmentErrors.Forbidden);
        }

        // 4. Build the metadata value object up front. FileMetadata.Create enforces the name/type/size
        //    invariants; the validator screened shape, so a throw here is a genuine edge (defense in depth).
        //    We do this BEFORE writing bytes so an invalid descriptor never causes a wasted blob write.
        FileMetadata fileMetadata;
        try
        {
            fileMetadata = FileMetadata.Create(request.FileName, request.ContentType, request.SizeInBytes);
        }
        catch (DomainException exception)
        {
            _logger.LogInformation(
                exception, "UploadAttachment rejected by domain invariant for task {TaskId}.", request.TaskId);
            return Result.Failure<UploadAttachmentResponse>(AttachmentErrors.Conflict(exception.Message));
        }

        // 5. Save the BYTES first and capture the opaque storage key. Ordering matters: if this fails,
        //    nothing was written to either store, so there is nothing to compensate.
        string storagePath;
        await using (var content = request.OpenReadStream())
        {
            storagePath = await _fileStorage.SaveAsync(content, request.FileName, cancellationToken);
        }

        var utcNow = _dateTimeProvider.UtcNow;
        var attachment = Attachment.Upload(request.TaskId, userId, fileMetadata, storagePath, utcNow);

        // 6. Persist the metadata row and commit. If the commit throws, run the COMPENSATING DELETE of the
        //    blob we wrote in step 5 so we do not leak an orphan, then rethrow to let the exception pipeline
        //    map it. The blob delete is idempotent, so a failed-then-retried commit path stays safe.
        try
        {
            await _attachments.AddAsync(attachment, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            _logger.LogError(
                "UploadAttachment: metadata commit failed for task {TaskId}; compensating by deleting blob {StoragePath}.",
                request.TaskId, storagePath);

            // Best-effort compensation. If the delete itself fails we log and let the original exception
            // surface — the orphan is now a job for the background sweeper, not a reason to mask the error.
            try
            {
                await _fileStorage.DeleteAsync(storagePath, cancellationToken);
            }
            catch (Exception compensationException)
            {
                _logger.LogError(
                    compensationException,
                    "UploadAttachment: compensating delete of blob {StoragePath} failed; it is now an orphan.",
                    storagePath);
            }

            throw;
        }

        _logger.LogInformation(
            "Attachment {AttachmentId} ({FileName}) uploaded to task {TaskId} by user {UserId}.",
            attachment.Id, fileMetadata.FileName, request.TaskId, userId);

        return new UploadAttachmentResponse(attachment.Id, fileMetadata.FileName, attachment.CreatedAtUtc);
    }
}
