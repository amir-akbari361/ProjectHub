using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Authentication;
using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Application.Common;

namespace ProjectHub.Application.Features.Authentication.RevokeToken;

/// <summary>
/// Handles <see cref="RevokeTokenCommand"/> — LOGOUT. It hashes the presented token, finds the owner,
/// and asks the aggregate to revoke exactly that grant. The whole flow is deliberately IDEMPOTENT and
/// SILENT: whether the token was valid, already revoked, or completely unknown, we return
/// <see cref="Result.Success"/>. Logout must never fail loudly (a user clicking "log out" doesn't care
/// why) and must never reveal whether a given token existed (enumeration hardening).
/// </summary>
public sealed class RevokeTokenCommandHandler : ICommandHandler<RevokeTokenCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ITokenHasher _tokenHasher;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RevokeTokenCommandHandler> _logger;

    public RevokeTokenCommandHandler(
        IApplicationDbContext context,
        ITokenHasher tokenHasher,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork,
        ILogger<RevokeTokenCommandHandler> logger)
    {
        _context = context;
        _tokenHasher = tokenHasher;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> Handle(RevokeTokenCommand request, CancellationToken cancellationToken)
    {
        // 1. Hash first — the DB stores only hashes, never raw tokens.
        var presentedHash = _tokenHasher.Hash(request.RefreshToken);

        // 2. Find the owning user (with the token collection loaded so the aggregate can mutate it).
        var user = await _context.Users
            .Include(u => u.RefreshTokens)
            .SingleOrDefaultAsync(
                u => u.RefreshTokens.Any(t => t.TokenHash == presentedHash),
                cancellationToken);

        // 3. Unknown token → still success. There is nothing to revoke, and reporting "not found" would
        //    leak that this exact token was never issued. Logout is best-effort; the client's intent
        //    (be logged out) is already satisfied.
        if (user is null)
        {
            return Result.Success();
        }

        // 4. Delegate to the aggregate. RevokeRefreshToken is itself idempotent — it no-ops if the token
        //    is already revoked/expired — so we don't need to branch here.
        user.RevokeRefreshToken(presentedHash, _dateTimeProvider.UtcNow);

        // 5. Persist the revocation. If nothing changed (already revoked), SaveChanges is a cheap no-op.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Refresh token revoked (logout) for user {UserId}", user.Id);

        return Result.Success();
    }
}
