using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Authentication;
using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Application.Common;
using ProjectHub.Domain.Entities;
using ProjectHub.Domain.ValueObjects;

namespace ProjectHub.Application.Features.Authentication.Login;

/// <summary>
/// Handles <see cref="LoginCommand"/> — authenticates the user's credentials and issues a token pair.
/// This is production security: we verify the password via constant-time BCrypt comparison, mint both
/// a signed JWT (access token) and a cryptographically-random refresh token, and persist only the
/// SHA-256 hash of the refresh token so a DB leak cannot be replayed. The client receives the raw
/// refresh token exactly once; we never see it again until the client presents it for rotation.
/// </summary>
public sealed class LoginCommandHandler : ICommandHandler<LoginCommand, LoginResponse>
{
    private readonly IRepository<User> _userRepository;
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtProvider _jwtProvider;
    private readonly ISecureTokenGenerator _tokenGenerator;
    private readonly ITokenHasher _tokenHasher;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LoginCommandHandler> _logger;

    public LoginCommandHandler(
        IRepository<User> userRepository,
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IJwtProvider jwtProvider,
        ISecureTokenGenerator tokenGenerator,
        ITokenHasher tokenHasher,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork,
        ILogger<LoginCommandHandler> logger)
    {
        _userRepository = userRepository;
        _context = context;
        _passwordHasher = passwordHasher;
        _jwtProvider = jwtProvider;
        _tokenGenerator = tokenGenerator;
        _tokenHasher = tokenHasher;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<LoginResponse>> Handle(
        LoginCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Parse the email into a value object so we can query with the exact representation EF uses.
        //    Email.Create normalizes to lowercase and trims whitespace, matching what the DB stores.
        var email = Email.Create(request.Email);

        // 2. Fetch the user by email. We need the full aggregate (with Roles loaded) because IJwtProvider
        //    embeds role claims into the JWT. EF translates `u.Email == email` into SQL via the
        //    HasConversion mapping in UserConfiguration, so this is a direct indexed lookup.
        var user = await _context.Users
            .Include(u => u.Roles)
            .SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

        // 3. Generic failure if the user doesn't exist. We return the SAME error as "wrong password" to
        //    prevent account enumeration — an attacker shouldn't learn whether an email is registered by
        //    observing different error messages. We still log the detail internally for ops debugging.
        if (user is null)
        {
            _logger.LogWarning("Login attempt for non-existent email: {Email}", email.Value);
            return Result.Failure<LoginResponse>(AuthErrors.InvalidCredentials);
        }

        // 4. Verify the password via IPasswordHasher.Verify. This is BCrypt's constant-time comparison —
        //    it hashes the candidate password and compares the result against the stored hash in a way
        //    that resists timing attacks. If verification fails, we again return the same generic error.
        var passwordValid = _passwordHasher.Verify(request.Password, user.PasswordHash);

        if (!passwordValid)
        {
            _logger.LogWarning("Login attempt with invalid password for user {UserId}", user.Id);
            return Result.Failure<LoginResponse>(AuthErrors.InvalidCredentials);
        }

        // 5. Additional business-rule checks. If the user's account is inactive, deny login even though
        //    their credentials are correct. This supports admin-driven account suspension.
        if (!user.IsActive)
        {
            _logger.LogWarning("Login attempt for inactive user {UserId}", user.Id);
            return Result.Failure<LoginResponse>(AuthErrors.AccountInactive);
        }

        // 6. Generate the access token (JWT). IJwtProvider is the port; the Infrastructure adapter wires
        //    in RSA-256 asymmetric signing so the API can verify tokens without sharing a secret key.
        //    The provider embeds user.Id, user.Email, and user.Roles as claims; the token expires in
        //    minutes (configured in Infrastructure), forcing the client to refresh frequently.
        var accessToken = _jwtProvider.GenerateAccessToken(user);

        // 7. Generate the refresh token. ISecureTokenGenerator uses a CSPRNG (RandomNumberGenerator) to
        //    produce a high-entropy, URL-safe random string. We hand the RAW token back to the client;
        //    they'll present it on every /refresh call. We persist only its SHA-256 hash so a DB dump
        //    cannot be replayed — the raw value is shown exactly once and then discarded from memory.
        var rawRefreshToken = _tokenGenerator.GenerateToken();
        var refreshTokenHash = _tokenHasher.Hash(rawRefreshToken);

        // 8. Calculate the refresh token's expiry. OAuth2 best practice: refresh tokens live much longer
        //    than access tokens (days or weeks vs. minutes) so the user gets a seamless session, but not
        //    so long that a stolen token is valid forever. We pick 7 days as a reasonable balance.
        var utcNow = _dateTimeProvider.UtcNow;
        var refreshTokenExpiresAt = utcNow.AddDays(7);

        // 9. Call the domain method to issue the refresh token. User.IssueRefreshToken enforces the
        //    invariant that only active users can receive tokens, creates the RefreshToken entity, and
        //    adds it to the user's collection. The RefreshToken tracks the hash, expiry, and creation
        //    time; it also has a ReplacedByTokenHash field (null initially) used for rotation detection.
        var refreshToken = user.IssueRefreshToken(refreshTokenHash, refreshTokenExpiresAt, utcNow);

        // 10. Persist the new refresh token. The repository stages the update; SaveChangesAsync commits
        //     the transaction. If another concurrent request inserts a conflicting token, the DB will
        //     reject it (unique index on TokenHash) and EF will throw DbUpdateException — the global
        //     exception handler maps that to a 500, which is correct for a concurrency bug.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "User {UserId} logged in successfully; refresh token expires at {ExpiresAt:O}",
            user.Id,
            refreshTokenExpiresAt);

        // 11. Return the token pair. The access token goes in the Authorization header as "Bearer <token>";
        //     the refresh token should be stored securely by the client (httpOnly cookie or secure storage).
        //     We echo back both expiry times so the client can proactively refresh the access token before
        //     it lapses, avoiding a race where an API call fails because the token expired mid-flight.
        return new LoginResponse(
            accessToken.Value,
            accessToken.ExpiresAtUtc,
            rawRefreshToken,
            refreshTokenExpiresAt);
    }
}
