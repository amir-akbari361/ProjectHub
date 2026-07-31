using FluentValidation;

namespace ProjectHub.Application.Features.Authentication.ConfirmEmail;

/// <summary>
/// Shape validation for <see cref="ConfirmEmailCommand"/>. Only presence is a SHAPE concern; whether
/// the token is real, unexpired, and unused are RUNTIME/business checks the handler + aggregate own.
/// </summary>
public sealed class ConfirmEmailValidator : AbstractValidator<ConfirmEmailCommand>
{
    public ConfirmEmailValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Confirmation token is required.");
    }
}
