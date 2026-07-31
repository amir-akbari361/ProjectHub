using FluentValidation;

namespace ProjectHub.Application.Features.Authentication.Login;

/// <summary>
/// Input validation for <see cref="LoginCommand"/>. Similar to RegisterUserValidator, this runs
/// BEFORE the handler executes so the handler can assume well-formed input and focus on the
/// business logic (credential verification, token minting). Duplicating the credential check here
/// would be redundant — the handler already queries the DB and verifies the password hash.
/// </summary>
public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email format is invalid.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}
