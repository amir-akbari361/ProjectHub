using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Application.Common;

namespace ProjectHub.Application.Features.Attachments.ListAttachments;

/// <summary>
/// Handles <see cref="ListAttachmentsQuery"/>. A READ-side handler that verifies the caller can see the
/// parent task's project, then projects the task's attachment metadata straight into
/// <see cref="AttachmentListItemResponse"/>. It NEVER materializes an <c>Attachment</c> aggregate and
/// NEVER touches the byte store — the list is pure SQL metadata; fetching bytes is the download endpoint's
/// job.
/// </summary>
/// <remarks>
/// WHY NEWEST-FIRST?
/// Unlike a comment thread (a conversation read oldest-first), an attachment list is a reference shelf: the
/// most recently added file is usually the one you want, so we order DESCENDING by upload time with Id as a
/// stable tiebreaker.
/// </remarks>
public sealed class ListAttachmentsQueryHandler
    : IQueryHandler<ListAttachmentsQuery, IReadOnlyList<AttachmentListItemResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<ListAttachmentsQueryHandler> _logger;

    public ListAttachmentsQueryHandler(
        IApplicationDbContext context,
        ICurrentUser currentUser,
        ILogger<ListAttachmentsQueryHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<AttachmentListItemResponse>>> Handle(
        ListAttachmentsQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Resolve the caller. Attachment lists are scoped to project membership, so an unauthenticated
        //    request has nothing it could legitimately see — fail fast with 401 before touching the DB.
        if (_currentUser.UserId is not { } userId)
        {
            _logger.LogWarning("ListAttachments reached the handler without an authenticated user.");
            return Result.Failure<IReadOnlyList<AttachmentListItemResponse>>(Error.Unauthorized(
                "Attachments.Unauthenticated",
                "You must be signed in to view attachments."));
        }

        // 2. Verify the caller can see the parent task's project. A cheap EXISTS over task -> project ->
        //    members. Unknown task or non-member both collapse into the SAME NotFound (no disclosure).
        var taskVisible = await _context.ProjectTasks
            .AsNoTracking()
            .Where(t => t.Id == request.TaskId)
            .AnyAsync(
                t => _context.Projects.Any(
                    p => p.Id == t.ProjectId && p.Members.Any(m => m.UserId == userId)),
                cancellationToken);

        if (!taskVisible)
        {
            _logger.LogInformation(
                "ListAttachments: task {TaskId} not found or not visible to user {UserId}.",
                request.TaskId, userId);
            return Result.Failure<IReadOnlyList<AttachmentListItemResponse>>(
                AttachmentErrors.TaskNotFound(request.TaskId));
        }

        // 3. Project this task's attachments straight into the lean DTO. AsNoTracking() — pure read; the
        //    global soft-delete filter already excludes deleted rows. StoragePath is deliberately NOT
        //    selected — it never leaves the server.
        var items = await _context.Attachments
            .AsNoTracking()
            .Where(a => a.TaskId == request.TaskId)
            .OrderByDescending(a => a.CreatedAtUtc)
            .ThenBy(a => a.Id)
            .Select(a => new AttachmentListItemResponse(
                a.Id,
                a.File.FileName,
                a.File.ContentType,
                a.File.SizeInBytes,
                a.UploadedBy,
                a.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Listed {Count} attachments for task {TaskId}.", items.Count, request.TaskId);

        return items;
    }
}
