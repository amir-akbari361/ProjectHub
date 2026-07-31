using FluentValidation;

namespace ProjectHub.Application.Features.Authentication.RevokeToken;

/// <summary>
/// Shape validation for <see cref="RevokeTokenCommand"/>. Identical in spirit to the refresh
/// validator: the token must be present, and everything else is a runtime/business concern.
/// </summary>
public sealed class RevokeTokenValidator : AbstractValidator<RevokeTokenCommand>
{
    public RevokeTokenValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("Refresh token is required.");
    }
}
