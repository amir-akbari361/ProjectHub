using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Authentication;
using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Application.Common;
using ProjectHub.Domain.Exceptions;

namespace ProjectHub.Application.Features.Authentication.RefreshToken;

/// <summary>
/// Handles <see cref="RefreshTokenCommand"/> — the token ROTATION flow. The client presents its raw
/// refresh token; we hash it, find the owning user, and delegate to <c>User.RotateRefreshToken</c>,
/// which revokes the old grant and issues a new one (with old→new linkage for reuse detection). We
/// then mint a fresh JWT so the client walks away with a brand-new pair. Rotation-on-every-use is the
/// OAuth2 best practice for public clients: a stolen token becomes useless the moment the legitimate
/// client next refreshes, and any replay of a consumed token trips the reuse alarm.
/// </summary>
public sealed class RefreshTokenCommandHandler : ICommandHandler<RefreshTokenCommand, RefreshTokenResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IJwtProvider _jwtProvider;
    private readonly ISecureTokenGenerator _tokenGenerator;
    private readonly ITokenHasher _tokenHasher;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RefreshTokenCommandHandler> _logger;

    public RefreshTokenCommandHandler(
        IApplicationDbContext context,
        IJwtProvider jwtProvider,
        ISecureTokenGenerator tokenGenerator,
        ITokenHasher tokenHasher,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork,
        ILogger<RefreshTokenCommandHandler> logger)
    {
        _context = context;
        _jwtProvider = jwtProvider;
        _tokenGenerator = tokenGenerator;
        _tokenHasher = tokenHasher;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<RefreshTokenResponse>> Handle(
        RefreshTokenCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Hash the presented raw token. We never query by the raw value — only hashes exist at rest,
        //    so we must hash first, then look up. SHA-256 is deterministic, so the same raw token always
        //    produces the same hash and can be matched against the stored one.
        var presentedHash = _tokenHasher.Hash(request.RefreshToken);

        // 2. Resolve the OWNING user from the token hash. We load the full RefreshTokens collection and
        //    the Roles (needed to re-mint the JWT). Loading the whole collection — not just the matching
        //    row — is intentional: RotateRefreshToken needs the collection so it can revoke the entire
        //    chain if it detects a reuse attack. We match the user via .Any on the child collection.
        var user = await _context.Users
            .Include(u => u.Roles)
            .Include(u => u.RefreshTokens)
            .SingleOrDefaultAsync(
                u => u.RefreshTokens.Any(t => t.TokenHash == presentedHash),
                cancellationToken);

        // 3. Unknown token → generic 401. No user owns this hash, so either it was never issued or it
        //    belongs to a deleted account. Either way the client must sign in again.
        if (user is null)
        {
            _logger.LogWarning("Refresh attempted with an unknown token hash.");
            return Result.Failure<RefreshTokenResponse>(AuthErrors.InvalidRefreshToken);
        }

        // 4. Generate the replacement token BEFORE calling the domain, because rotation needs the new
        //    hash to record the old→new link. The raw value is returned to the client exactly once.
        var utcNow = _dateTimeProvider.UtcNow;
        var newRawToken = _tokenGenerator.GenerateToken();
        var newHash = _tokenHasher.Hash(newRawToken);
        var newExpiresAt = utcNow.AddDays(7);

        try
        {
            // 5. Delegate to the aggregate. RotateRefreshToken enforces every rule: the token must
            //    exist and be active; if it's already revoked (a replay of a consumed token) it revokes
            //    the WHOLE chain and throws — turning a stolen token into a self-defeating weapon.
            user.RotateRefreshToken(presentedHash, newHash, newExpiresAt, utcNow);
        }
        catch (DomainException ex)
        {
            // 6. A domain rejection here is an EXPECTED business failure (expired/revoked/reused token),
            //    not a bug — so we translate it to a controlled 401 rather than letting it bubble to a
            //    500. Crucially, we still SaveChanges below-path only on success; but if reuse triggered
            //    a chain-wide revoke, those revocations were applied to the tracked entities, so we must
            //    persist them to actually lock the attacker out.
            _logger.LogWarning(ex, "Refresh rejected for user {UserId}: {Reason}", user.Id, ex.Message);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Failure<RefreshTokenResponse>(AuthErrors.InvalidRefreshToken);
        }

        // 7. Persist the rotation (old token revoked + new token inserted) atomically.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 8. Mint a fresh JWT for the (still-authenticated) user and return the new pair.
        var accessToken = _jwtProvider.GenerateAccessToken(user);

        _logger.LogInformation("Refresh token rotated for user {UserId}", user.Id);

        return new RefreshTokenResponse(
            accessToken.Value,
            accessToken.ExpiresAtUtc,
            newRawToken,
            newExpiresAt);
    }
}
