using FluentValidation;

namespace ProjectHub.Application.Features.Admin.Users.SetUserStatus;

/// <summary>Validates <see cref="DeactivateUserCommand"/>: the target id must actually be supplied.</summary>
public sealed class DeactivateUserValidator : AbstractValidator<DeactivateUserCommand>
{
    public DeactivateUserValidator()
    {
        RuleFor(c => c.UserId)
            .NotEmpty().WithMessage("User id is required.");
    }
}

/// <summary>Validates <see cref="ReactivateUserCommand"/>: the target id must actually be supplied.</summary>
public sealed class ReactivateUserValidator : AbstractValidator<ReactivateUserCommand>
{
    public ReactivateUserValidator()
    {
        RuleFor(c => c.UserId)
            .NotEmpty().WithMessage("User id is required.");
    }
}
