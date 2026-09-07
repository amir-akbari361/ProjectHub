using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Application.Common;

namespace ProjectHub.Application.Features.Admin.Users.SetUserStatus;

/// <summary>
/// Handles <see cref="DeactivateUserCommand"/>. Loads the target account tracked, refuses the self-lockout
/// case, delegates the state change to <c>User.Deactivate</c>, and commits once.
/// </summary>
/// <remarks>
/// WHY IS "ALREADY INACTIVE" CHECKED HERE WHEN THE DOMAIN ALSO GUARDS IT?
/// <c>User.Deactivate</c> throws a <c>DomainException</c> for a no-op, which would surface as a generic
/// server-side fault rather than a useful message. Checking first lets this return a modelled 409 that names
/// the situation, and keeps the exception channel for genuinely unexpected states. The domain guard stays as
/// the invariant's last line of defence for any other caller.
///
/// WHY REFUSE TO DEACTIVATE YOURSELF?
/// It is the one case an administrator cannot undo through the UI: the next token refresh rejects them, and
/// the console they would need is behind the very access they just removed. Another Admin can still do it, so
/// the account is not undeactivatable — it just cannot be done by accident to oneself.
/// </remarks>
public sealed class DeactivateUserCommandHandler : ICommandHandler<DeactivateUserCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeactivateUserCommandHandler> _logger;

    public DeactivateUserCommandHandler(
        IApplicationDbContext context,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork,
        ILogger<DeactivateUserCommandHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> Handle(DeactivateUserCommand request, CancellationToken cancellationToken)
    {
        var access = AdminAccess.Resolve(_currentUser);

        if (access.IsFailure)
        {
            _logger.LogWarning("DeactivateUser was refused: {ErrorCode}.", access.Error.Code);
            return Result.Failure(access.Error);
        }

        if (request.UserId == access.Value)
        {
            _logger.LogWarning("Admin {AdminId} attempted to deactivate their own account.", access.Value);
            return Result.Failure(AdminUserErrors.CannotDeactivateSelf);
        }

        var user = await _context.Users
            .SingleOrDefaultAsync(candidate => candidate.Id == request.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(AdminUserErrors.UserNotFound(request.UserId));
        }

        if (!user.IsActive)
        {
            return Result.Failure(AdminUserErrors.AlreadyInactive);
        }

        user.Deactivate(_dateTimeProvider.UtcNow, access.Value);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Admin {AdminId} deactivated user {UserId}.", access.Value, request.UserId);

        return Result.Success();
    }
}
