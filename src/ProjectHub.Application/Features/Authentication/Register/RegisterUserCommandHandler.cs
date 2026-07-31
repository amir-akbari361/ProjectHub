using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Authentication;
using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Application.Common;
using ProjectHub.Domain.Entities;
using ProjectHub.Domain.ValueObjects;

namespace ProjectHub.Application.Features.Authentication.Register;

/// <summary>
/// Handles <see cref="RegisterUserCommand"/> — the orchestrator for user registration. This
/// embodies the "thin controller, fat domain" principle: the handler coordinates infrastructure
/// services (password hasher, DB context) but delegates invariant enforcement to the domain
/// (<see cref="User.Register"/> and <see cref="Email.Create"/> already guarantee validity). Every
/// port (IPasswordHasher, IRepository) is injected so the handler is 100% testable without EF or
/// BCrypt running; the production adapters get swapped in via DI at the composition root.
/// </summary>
public sealed class RegisterUserCommandHandler
    : ICommandHandler<RegisterUserCommand, RegisterUserResponse>
{
    private readonly IRepository<User> _userRepository;
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RegisterUserCommandHandler> _logger;

    public RegisterUserCommandHandler(
        IRepository<User> userRepository,
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork,
        ILogger<RegisterUserCommandHandler> logger)
    {
        _userRepository = userRepository;
        _context = context;
        _passwordHasher = passwordHasher;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<RegisterUserResponse>> Handle(
        RegisterUserCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Parse email into a value object. The Email.Create factory performs structural validation
        //    and normalization (lowercase, trim). If the format is invalid, it throws DomainException.
        //    We let the UnhandledExceptionBehavior catch that and map it to a 500, which is deliberate:
        //    FluentValidation's EmailAddress rule SHOULD have caught malformed input before we reached
        //    this line. If we still hit a DomainException here, it's a validation-gap bug — a 500 is
        //    correct because the pipeline failed to do its job.
        var email = Email.Create(request.Email);

        // 2. Check uniqueness. This is a BUSINESS rule (you can't register twice), not a SHAPE rule,
        //    so it belongs in the handler, not in FluentValidation. Doing it in FluentValidation would
        //    cause a redundant DB query and a TOCTOU race: another request could insert the same email
        //    between validation and the handler, causing a PK violation. We accept the race and return
        //    a controlled Conflict error instead of letting SQL throw a DbUpdateException.
        var emailExists = await _context.Users
            .AnyAsync(u => u.Email == email, cancellationToken);

        if (emailExists)
        {
            // The message is deliberately generic to avoid confirming which emails are taken (account
            // enumeration hardening). A sophisticated attacker can still probe via timing, but we don't
            // make it trivial by echoing "user@example.com is already registered."
            _logger.LogWarning("Registration attempt for existing email: {Email}", email.Value);
            return Result.Failure<RegisterUserResponse>(AuthErrors.EmailAlreadyInUse);
        }

        // 3. Hash the password. This is infrastructure I/O (BCrypt with a work factor), so we delegate
        //    to the IPasswordHasher port. The raw password is never persisted or logged — it exists
        //    in memory only until BCrypt consumes it. The resulting hash is safe to store and log.
        var passwordHash = _passwordHasher.Hash(request.Password);

        // 4. Construct the aggregate via the domain factory. User.Register enforces every invariant
        //    (non-empty names, valid email, non-null hash) and raises the UserRegisteredDomainEvent.
        //    Notice that the domain never knows about IPasswordHasher — the hash is just a string to
        //    the domain. This preserves the layering: Domain is pure business logic; Application is
        //    the orchestrator that wires infrastructure into domain workflows.
        var utcNow = _dateTimeProvider.UtcNow;
        var user = User.Register(email, request.FirstName, request.LastName, passwordHash, utcNow);

        // 5. Persist the aggregate. The repository stages the insert; SaveChangesAsync flushes it to
        //    the DB and dispatches the UserRegisteredDomainEvent via PublishDomainEventsInterceptor.
        await _userRepository.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} registered successfully with email {Email}", user.Id, email.Value);

        // 6. Return the response DTO. We echo back the id and the NORMALIZED email (lowercase) so the
        //    client knows exactly what was stored. This is the anti-corruption layer in action: the
        //    API/UI see only RegisterUserResponse, never the full User aggregate. If User grows a new
        //    field (e.g., ProfilePictureUrl), the response DTO contract stays unchanged unless we
        //    deliberately extend it — preventing accidental breaking changes.
        return new RegisterUserResponse(user.Id, user.Email.Value);
    }
}
