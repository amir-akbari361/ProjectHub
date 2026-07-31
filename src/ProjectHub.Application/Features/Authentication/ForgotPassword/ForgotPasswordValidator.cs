using FluentValidation;

namespace ProjectHub.Application.Features.Authentication.ForgotPassword;

/// <summary>
/// Shape validation for <see cref="ForgotPasswordCommand"/>. We check the email is present and looks
/// like an email — a malformed address can never match an account, so rejecting it early avoids a
/// pointless DB round-trip. Note this is the ONLY visible way the endpoint can "fail": a well-formed
/// but non-existent address still returns success (enumeration hardening lives in the handler).
/// </summary>
public sealed class ForgotPasswordValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");
    }
}
