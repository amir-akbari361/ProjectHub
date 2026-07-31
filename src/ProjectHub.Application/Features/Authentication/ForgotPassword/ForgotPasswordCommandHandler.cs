using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Authentication;
using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Application.Common;
using ProjectHub.Domain.Enums;
using ProjectHub.Domain.ValueObjects;

namespace ProjectHub.Application.Features.Authentication.ForgotPassword;

/// <summary>
/// Handles <see cref="ForgotPasswordCommand"/> — the "send me a reset link" step. The defining design
/// rule is UNCONDITIONAL SUCCESS: whether or not the email maps to a real account, we return
/// <see cref="Result.Success"/>. Only when a matching, active user is found do we actually mint a
/// reset token and dispatch the email. This makes the endpoint useless as an enumeration oracle while
/// still doing the right thing for real users.
/// </summary>
public sealed class ForgotPasswordCommandHandler : ICommandHandler<ForgotPasswordCommand>
{
    // Reset links are the highest-value email tokens (they grant password change), so we keep the
    // window short. One hour balances "user checks email later" against "stolen link stays usable".
    private static readonly TimeSpan ResetTokenLifetime = TimeSpan.FromHours(1);

    private readonly IApplicationDbContext _context;
    private readonly ISecureTokenGenerator _tokenGenerator;
    private readonly ITokenHasher _tokenHasher;
    private readonly IEmailSender _emailSender;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ForgotPasswordCommandHandler> _logger;

    public ForgotPasswordCommandHandler(
        IApplicationDbContext context,
        ISecureTokenGenerator tokenGenerator,
        ITokenHasher tokenHasher,
        IEmailSender emailSender,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork,
        ILogger<ForgotPasswordCommandHandler> logger)
    {
        _context = context;
        _tokenGenerator = tokenGenerator;
        _tokenHasher = tokenHasher;
        _emailSender = emailSender;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        // 1. Normalize the input the same way registration did, so lookups match regardless of casing.
        var email = Email.Create(request.Email);

        // 2. Look up the owner, loading UserTokens so the aggregate can invalidate any prior live reset
        //    token before issuing a new one. We only need the ACTIVE user — a deactivated account
        //    shouldn't be resettable — but we branch silently rather than erroring.
        var user = await _context.Users
            .Include(u => u.UserTokens)
            .SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

        // 3. No account (or an inactive one) → return success WITHOUT sending anything. This is the
        //    enumeration-hardening branch: the caller cannot distinguish this from the happy path.
        if (user is null || !user.IsActive)
        {
            _logger.LogInformation("Password reset requested for an unknown or inactive email; no action taken.");
            return Result.Success();
        }

        // 4. Generate the raw token (CSPRNG) and its hash. The raw value goes into the emailed link and
        //    is never stored; only the hash is persisted, so a DB leak can't be used to reset passwords.
        var utcNow = _dateTimeProvider.UtcNow;
        var rawToken = _tokenGenerator.GenerateToken();
        var tokenHash = _tokenHasher.Hash(rawToken);
        var expiresAt = utcNow.Add(ResetTokenLifetime);

        // 5. Mint through the aggregate. IssueToken consumes any still-live reset token first, enforcing
        //    "at most one active reset link per user" — so an attacker can't stockpile valid links.
        user.IssueToken(tokenHash, UserTokenType.PasswordReset, expiresAt, utcNow);

        // 6. Persist the token BEFORE sending the email. If the email send fails we've still recorded a
        //    valid token (the user can retry), whereas emailing first then failing to save would hand
        //    out a link that matches nothing.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 7. Dispatch the reset email through the port. The handler builds only the raw token; assembling
        //    the actual clickable URL (base address, route) is the Infrastructure adapter's concern.
        await _emailSender.SendPasswordResetAsync(user.Email.Value, rawToken, cancellationToken);

        _logger.LogInformation("Password reset link issued for user {UserId}.", user.Id);

        return Result.Success();
    }
}
