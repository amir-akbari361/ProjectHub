using FluentValidation;

namespace ProjectHub.Application.Features.Authentication.ResetPassword;

/// <summary>
/// Shape validation for <see cref="ResetPasswordCommand"/>. The token must be present, and the new
/// password must satisfy the SAME strength policy as registration. Keeping these rules identical is a
/// DRY/consistency concern: it would be a security hole to enforce strong passwords at sign-up but
/// allow a weak one through the reset path. (When rules like this appear a third time, extract a
/// shared <c>PasswordRules</c> extension — for two call sites duplication is still acceptable.)
/// </summary>
public sealed class ResetPasswordValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Reset token is required.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .MaximumLength(128).WithMessage("Password cannot exceed 128 characters.")
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.");
    }
}
