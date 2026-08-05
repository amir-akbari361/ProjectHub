using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Application.Common;
using ProjectHub.Domain.Entities;
using ProjectHub.Domain.Enums;
using ProjectHub.Domain.Exceptions;
using ProjectHub.Domain.ValueObjects;

namespace ProjectHub.Application.Features.Comments.AddComment;

/// <summary>
/// Handles <see cref="AddCommentCommand"/>. A WRITE-side handler that verifies the parent task exists and
/// is visible to the caller, authorizes the caller against the task's project membership, constructs the
/// <c>Comment</c> aggregate through its factory (which raises CommentAddedDomainEvent), persists it via the
/// generic write repository, and commits once through the <see cref="IUnitOfWork"/>.
/// </summary>
/// <remarks>
/// WHY RESOLVE THE PROJECT VIA THE TASK?
/// Comments hang off tasks, but authorization is defined at the PROJECT level (membership + role). A task
/// alone doesn't carry the membership list, so we walk task -> project to fetch the caller's role. We do
/// this as a single read-only projection rather than materializing either aggregate.
///
/// WHY A Contributor MINIMUM?
/// A Viewer can read the discussion but not participate. Posting is a mutating, attributed action, so it
/// requires at least Contributor — matching the bar used by the task write handlers for consistency.
/// </remarks>
public sealed class AddCommentCommandHandler : ICommandHandler<AddCommentCommand, AddCommentResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IRepository<Comment> _comments;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AddCommentCommandHandler> _logger;

    public AddCommentCommandHandler(
        IApplicationDbContext context,
        IRepository<Comment> comments,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork,
        ILogger<AddCommentCommandHandler> logger)
    {
        _context = context;
        _comments = comments;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<AddCommentResponse>> Handle(
        AddCommentCommand request, CancellationToken cancellationToken)
    {
        // 1. Resolve the caller. Posting is attributed and authorized, so no principal => 401.
        if (_currentUser.UserId is not { } userId)
        {
            _logger.LogWarning("AddComment reached the handler without an authenticated user.");
            return Result.Failure<AddCommentResponse>(Error.Unauthorized(
                "Comments.Unauthenticated",
                "You must be signed in to comment."));
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
                "AddComment: task {TaskId} not found for user {UserId}.", request.TaskId, userId);
            return Result.Failure<AddCommentResponse>(CommentErrors.TaskNotFound(request.TaskId));
        }

        // 3. Authorize against the project's membership. Read-only projection of just the caller's role.
        //    A non-member gets the SAME NotFound as an unknown task (no disclosure); a Viewer gets 403.
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
                "AddComment: user {UserId} is not a member of project {ProjectId}.", userId, projectId);
            return Result.Failure<AddCommentResponse>(CommentErrors.TaskNotFound(request.TaskId));
        }

        if (callerRole < ProjectRole.Contributor)
        {
            _logger.LogInformation(
                "AddComment: user {UserId} with role {Role} may not comment in project {ProjectId}.",
                userId, callerRole, projectId);
            return Result.Failure<AddCommentResponse>(CommentErrors.Forbidden);
        }

        // 4. Build the value object. CommentBody.Create trims and enforces the length invariant; the
        //    validator already screened shape, so a throw here is a genuine edge (defense in depth).
        var utcNow = _dateTimeProvider.UtcNow;

        Comment comment;
        try
        {
            var body = CommentBody.Create(request.Body);
            comment = Comment.Create(request.TaskId, userId, body, utcNow);
        }
        catch (DomainException exception)
        {
            _logger.LogInformation(
                exception, "AddComment rejected by domain invariant for task {TaskId}.", request.TaskId);
            return Result.Failure<AddCommentResponse>(CommentErrors.Conflict(exception.Message));
        }

        // 5. Stage the insert and commit once. The domain event raised in the factory is dispatched by
        //    the SaveChanges interceptor inside the same transaction.
        await _comments.AddAsync(comment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Comment {CommentId} added to task {TaskId} by user {UserId}.",
            comment.Id, request.TaskId, userId);

        return new AddCommentResponse(comment.Id, comment.CreatedAtUtc);
    }
}
