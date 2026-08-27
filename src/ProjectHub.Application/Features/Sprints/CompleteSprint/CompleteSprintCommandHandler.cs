using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Application.Common;
using ProjectHub.Domain.Enums;
using ProjectHub.Domain.Exceptions;

namespace ProjectHub.Application.Features.Sprints.CompleteSprint;

/// <summary>
/// Handles <see cref="CompleteSprintCommand"/>. A WRITE-side handler that authorizes the caller against
/// the sprint's parent project, delegates the transition to <c>Sprint.Complete</c> (which guards that the
/// sprint is Active and stamps audit fields), and commits once. Mirrors <c>StartSprintCommandHandler</c>
/// exactly, differing only in the domain method it calls.
/// </summary>
/// <remarks>
/// WHY CATCH <c>DomainException</c> HERE?
/// "The sprint is not Active" (e.g., still Planned, or already Completed) is an EXPECTED business outcome.
/// We translate the domain guard into a modeled 409 Conflict so the client gets a clean, typed error
/// rather than a 500, keeping the exception channel reserved for the truly unexpected.
/// </remarks>
public sealed class CompleteSprintCommandHandler : ICommandHandler<CompleteSprintCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CompleteSprintCommandHandler> _logger;

    public CompleteSprintCommandHandler(
        IApplicationDbContext context,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork,
        ILogger<CompleteSprintCommandHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> Handle(
        CompleteSprintCommand request, CancellationToken cancellationToken)
    {
        // 1. Resolve the caller. Completing a sprint is attributed and authorized, so no principal => 401.
        if (_currentUser.UserId is not { } userId)
        {
            _logger.LogWarning("CompleteSprint reached the handler without an authenticated user.");
            return Result.Failure(Error.Unauthorized(
                "Sprints.Unauthenticated",
                "You must be signed in to complete a sprint."));
        }

        // 2. Load the sprint TRACKED — we are going to mutate it.
        var sprint = await _context.Sprints
            .SingleOrDefaultAsync(s => s.Id == request.SprintId, cancellationToken);

        if (sprint is null)
        {
            _logger.LogInformation(
                "CompleteSprint: sprint {SprintId} not found for user {UserId}.",
                request.SprintId, userId);
            return Result.Failure(SprintErrors.NotFound(request.SprintId));
        }

        // 3. Authorize against the parent project's membership. Read-only projection: we only need the
        //    caller's role. A non-member gets 404 (no disclosure); an under-privileged member gets 403.
        var callerRole = await _context.Projects
            .AsNoTracking()
            .Where(p => p.Id == sprint.ProjectId)
            .SelectMany(p => p.Members)
            .Where(m => m.UserId == userId)
            .Select(m => (ProjectRole?)m.Role)
            .SingleOrDefaultAsync(cancellationToken);

        if (callerRole is null)
        {
            _logger.LogInformation(
                "CompleteSprint: user {UserId} is not a member of project {ProjectId}.",
                userId, sprint.ProjectId);
            return Result.Failure(SprintErrors.NotFound(request.SprintId));
        }

        if (callerRole < ProjectRole.Contributor)
        {
            _logger.LogInformation(
                "CompleteSprint: user {UserId} with role {Role} may not complete sprints.",
                userId, callerRole);
            return Result.Failure(SprintErrors.Forbidden);
        }

        // 4. Delegate to the domain. Complete throws if the sprint is not Active; we translate that guard
        //    into a modeled 409 instead of letting it become a 500.
        try
        {
            sprint.Complete(_dateTimeProvider.UtcNow, userId);
        }
        catch (DomainException exception)
        {
            _logger.LogInformation(
                exception, "CompleteSprint rejected by domain invariant for sprint {SprintId}.",
                request.SprintId);
            return Result.Failure(SprintErrors.Conflict(exception.Message));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Sprint {SprintId} completed by user {UserId}.", request.SprintId, userId);

        return Result.Success();
    }
}
