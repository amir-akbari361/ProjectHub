using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Authentication;
using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Application.Common;
using ProjectHub.Domain.Enums;

namespace ProjectHub.Application.Features.Authentication.ConfirmEmail;

/// <summary>
/// Handles <see cref="ConfirmEmailCommand"/> — email verification. It hashes the presented token,
/// finds the owning user, and delegates to <c>User.ConfirmEmailWithToken</c>, which enforces every
/// rule (right type, unexpired, unused, not-already-confirmed) inside the aggregate. On any failure
/// we return ONE generic error so a probing client can't tell "wrong token" from "already confirmed".
/// </summary>
public sealed class ConfirmEmailCommandHandler : ICommandHandler<ConfirmEmailCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ITokenHasher _tokenHasher;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ConfirmEmailCommandHandler> _logger;

    public ConfirmEmailCommandHandler(
        IApplicationDbContext context,
        ITokenHasher tokenHasher,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork,
        ILogger<ConfirmEmailCommandHandler> logger)
    {
        _context = context;
        _tokenHasher = tokenHasher;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> Handle(ConfirmEmailCommand request, CancellationToken cancellationToken)
    {
        // 1. Hash first — the DB stores only hashes, never raw tokens.
        var presentedHash = _tokenHasher.Hash(request.Token);

        // 2. Resolve the owning user from the token hash, filtering by TYPE so a confirmation link can
        //    only ever match a confirmation token. We load the UserTokens collection so the aggregate
        //    can consume the matched token. Matching by type here also lets the composite
        //    (UserId, Type) index help, and prevents a reset token from being mistaken for this flow.
        var user = await _context.Users
            .Include(u => u.UserTokens)
            .SingleOrDefaultAsync(
                u => u.UserTokens.Any(t =>
                    t.TokenHash == presentedHash && t.Type == UserTokenType.EmailConfirmation),
                cancellationToken);

        // 3. Unknown token → generic failure. Either it was never issued or belongs to a purged account.
        if (user is null)
        {
            _logger.LogWarning("Email confirmation attempted with an unknown token hash.");
            return Result.Failure(AuthErrors.InvalidEmailConfirmationToken);
        }

        // 4. Delegate to the aggregate. It re-checks redeemability (unexpired, unused) and the
        //    already-confirmed guard, consuming the token and raising UserEmailConfirmedDomainEvent on
        //    success. A false return means the token existed but couldn't be redeemed right now.
        var confirmed = user.ConfirmEmailWithToken(presentedHash, _dateTimeProvider.UtcNow);

        if (!confirmed)
        {
            _logger.LogWarning("Email confirmation rejected for user {UserId} (expired/used/already confirmed).", user.Id);
            return Result.Failure(AuthErrors.InvalidEmailConfirmationToken);
        }

        // 5. Persist the consumption + IsEmailConfirmed flip, and dispatch the domain event.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Email confirmed for user {UserId}.", user.Id);

        return Result.Success();
    }
}
