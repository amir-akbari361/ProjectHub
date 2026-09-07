using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Application.Common;

namespace ProjectHub.Application.Features.Admin.Users.SetUserStatus;

/// <summary>
/// Handles <see cref="ReactivateUserCommand"/>. The mirror of the deactivation handler: load tracked, reject
/// the no-op with a modelled 409 rather than letting the domain throw, delegate to <c>User.Reactivate</c>,
/// and commit once.
/// </summary>
/// <remarks>
/// There is no self-targeting guard here, and none is needed: reactivating an account only ever GRANTS
/// access, so no administrator can lock themselves out with it. (Reaching this code at all means the caller
/// is an active Admin, so their own account is active by definition and the domain would refuse anyway.)
/// </remarks>
public sealed class ReactivateUserCommandHandler : ICommandHandler<ReactivateUserCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ReactivateUserCommandHandler> _logger;

    public ReactivateUserCommandHandler(
        IApplicationDbContext context,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork,
        ILogger<ReactivateUserCommandHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> Handle(ReactivateUserCommand request, CancellationToken cancellationToken)
    {
        var access = AdminAccess.Resolve(_currentUser);

        if (access.IsFailure)
        {
            _logger.LogWarning("ReactivateUser was refused: {ErrorCode}.", access.Error.Code);
            return Result.Failure(access.Error);
        }

        var user = await _context.Users
            .SingleOrDefaultAsync(candidate => candidate.Id == request.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(AdminUserErrors.UserNotFound(request.UserId));
        }

        if (user.IsActive)
        {
            return Result.Failure(AdminUserErrors.AlreadyActive);
        }

        user.Reactivate(_dateTimeProvider.UtcNow, access.Value);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Admin {AdminId} reactivated user {UserId}.", access.Value, request.UserId);

        return Result.Success();
    }
}
