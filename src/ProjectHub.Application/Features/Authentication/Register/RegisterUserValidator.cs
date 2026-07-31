using FluentValidation;

namespace ProjectHub.Application.Features.Authentication.Register;

/// <summary>
/// Input validation for <see cref="RegisterUserCommand"/>. This runs inside the MediatR
/// ValidationBehavior BEFORE the handler executes — so the handler can assume its input is
/// structurally valid and focus purely on business rules (e.g., "email already taken").
///
/// The split of responsibilities is deliberate:
///  • FluentValidation = cheap, stateless, SHAPE checks (required, length, format).
///  • Handler          = expensive, stateful, BUSINESS checks (uniqueness needs a DB round-trip).
/// Duplicating the uniqueness check here would cause a redundant query and a TOCTOU race anyway.
/// </summary>
public sealed class RegisterUserValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .MaximumLength(320).WithMessage("Email cannot exceed 320 characters.")
            .EmailAddress().WithMessage("Email format is invalid.");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(100).WithMessage("First name cannot exceed 100 characters.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(100).WithMessage("Last name cannot exceed 100 characters.");

        // NIST 800-63B favors length over arbitrary composition rules; we require a strong minimum
        // and a modest ceiling to bound BCrypt cost (BCrypt only hashes the first 72 bytes anyway).
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .MaximumLength(128).WithMessage("Password cannot exceed 128 characters.")
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.");
    }
}
