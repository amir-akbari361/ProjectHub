using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Application.Common;
using ProjectHub.Domain.Exceptions;
using ProjectHub.Domain.ValueObjects;

namespace ProjectHub.Application.Features.Comments.EditComment;

/// <summary>
/// Handles <see cref="EditCommentCommand"/>. A WRITE-side handler that loads the comment TRACKED, confirms
/// the caller can still see the parent task's project (defense against editing after being removed from a
/// project), delegates the mutation to <c>Comment.Edit</c> — which enforces the "only the author may edit"
/// invariant and raises CommentEditedDomainEvent — and commits once.
/// </summary>
/// <remarks>
/// WHY LET THE DOMAIN OWN THE AUTHORSHIP CHECK?
/// "Only the author may edit" is a true business invariant of the Comment aggregate, so it belongs INSIDE
/// the aggregate (Comment.Edit throws a DomainException on mismatch), not duplicated in the handler. The
/// handler's job is orchestration + translating that domain guard into a modeled 403, so the client gets a
/// clean typed error instead of a 500. We still do a membership pre-check to collapse "not visible" into a
/// 404 without disclosing the comment's existence.
/// </remarks>
public sealed class EditCommentCommandHandler : ICommandHandler<EditCommentCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<EditCommentCommandHandler> _logger;

    public EditCommentCommandHandler(
        IApplicationDbContext context,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork,
        ILogger<EditCommentCommandHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> Handle(EditCommentCommand request, CancellationToken cancellationToken)
    {
        // 1. Resolve the caller. Editing is attributed and authorized, so no principal => 401.
        if (_currentUser.UserId is not { } userId)
        {
            _logger.LogWarning("EditComment reached the handler without an authenticated user.");
            return Result.Failure(Error.Unauthorized(
                "Comments.Unauthenticated",
                "You must be signed in to edit a comment."));
        }

        // 2. Load the comment TRACKED — we are going to mutate it, so EF must observe the change.
        var comment = await _context.Comments
            .SingleOrDefaultAsync(c => c.Id == request.CommentId, cancellationToken);

        if (comment is null)
        {
            _logger.LogInformation(
                "EditComment: comment {CommentId} not found for user {UserId}.",
                request.CommentId, userId);
            return Result.Failure(CommentErrors.NotFound(request.CommentId));
        }

        // 3. Confirm the caller can still see the parent task's project. Being the author is not enough if
        //    they've since been removed from the project. A cheap EXISTS over task -> project -> members.
        var visible = await _context.ProjectTasks
            .AsNoTracking()
            .Where(t => t.Id == comment.TaskId)
            .AnyAsync(
                t => _context.Projects.Any(
                    p => p.Id == t.ProjectId && p.Members.Any(m => m.UserId == userId)),
                cancellationToken);

        if (!visible)
        {
            _logger.LogInformation(
                "EditComment: comment {CommentId} not visible to user {UserId}.",
                request.CommentId, userId);
            return Result.Failure(CommentErrors.NotFound(request.CommentId));
        }

        // 4. Delegate to the domain. Comment.Edit throws if the editor is not the author; we translate
        //    that invariant into a modeled 403 rather than letting it become a 500.
        try
        {
            var newBody = CommentBody.Create(request.Body);
            comment.Edit(newBody, userId, _dateTimeProvider.UtcNow);
        }
        catch (DomainException exception)
        {
            _logger.LogInformation(
                exception, "EditComment rejected by domain invariant for comment {CommentId}.",
                request.CommentId);
            return Result.Failure(CommentErrors.Forbidden);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Comment {CommentId} edited by user {UserId}.", request.CommentId, userId);

        return Result.Success();
    }
}
