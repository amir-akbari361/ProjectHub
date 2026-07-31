using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Authentication;
using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Application.Common;
using ProjectHub.Domain.Enums;

namespace ProjectHub.Application.Features.Authentication.ResetPassword;

/// <summary>
/// Handles <see cref="ResetPasswordCommand"/> — the final step of the reset flow. It hashes the
/// presented token, finds the owner, hashes the NEW password, and delegates to
/// <c>User.ResetPasswordWithToken</c>, which consumes the token, swaps the hash, and — critically —
/// revokes EVERY refresh token so any attacker who already had a session is kicked out. On any token
/// failure we return one generic error (enumeration hardening), exactly like email confirmation.
/// </summary>
public sealed class ResetPasswordCommandHandler : ICommandHandler<ResetPasswordCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenHasher _tokenHasher;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ResetPasswordCommandHandler> _logger;

    public ResetPasswordCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        ITokenHasher tokenHasher,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork,
        ILogger<ResetPasswordCommandHandler> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _tokenHasher = tokenHasher;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        // 1. Hash the presented token — only hashes exist at rest.
        var presentedHash = _tokenHasher.Hash(request.Token);

        // 2. Resolve the owner, filtered to PasswordReset tokens so a confirmation link can never be
        //    redeemed here. We load BOTH child collections: UserTokens (to consume the reset token) and
        //    RefreshTokens (so the aggregate can revoke every live session as part of the reset).
        var user = await _context.Users
            .Include(u => u.UserTokens)
            .Include(u => u.RefreshTokens)
            .SingleOrDefaultAsync(
                u => u.UserTokens.Any(t =>
                    t.TokenHash == presentedHash && t.Type == UserTokenType.PasswordReset),
                cancellationToken);

        // 3. Unknown token → generic failure.
        if (user is null)
        {
            _logger.LogWarning("Password reset attempted with an unknown token hash.");
            return Result.Failure(AuthErrors.InvalidPasswordResetToken);
        }

        // 4. Hash the new password BEFORE touching the aggregate — the domain only ever sees a hash,
        //    never the plaintext. This keeps BCrypt (infrastructure) out of the domain layer.
        var newPasswordHash = _passwordHasher.Hash(request.NewPassword);

        // 5. Delegate to the aggregate. ResetPasswordWithToken re-validates redeemability, consumes the
        //    token, replaces the hash, revokes all refresh tokens, and raises UserPasswordResetDomainEvent.
        //    A false return means the token existed but was expired/already used.
        var reset = user.ResetPasswordWithToken(presentedHash, newPasswordHash, _dateTimeProvider.UtcNow);

        if (!reset)
        {
            _logger.LogWarning("Password reset rejected for user {UserId} (expired or already-used token).", user.Id);
            return Result.Failure(AuthErrors.InvalidPasswordResetToken);
        }

        // 6. Persist the new hash + token consumption + session revocations atomically, dispatching the
        //    domain event so downstream handlers (e.g., "your password was changed" email) can react.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Password reset completed for user {UserId}; all sessions revoked.", user.Id);

        return Result.Success();
    }
}
