using FluentValidation;

namespace ProjectHub.Application.Features.Authentication.RefreshToken;

/// <summary>
/// Shape validation for <see cref="RefreshTokenCommand"/>. There is only one field, and the only
/// thing we can meaningfully assert BEFORE touching the database is that it is present. Whether the
/// token is genuine, unexpired, and not already rotated is a BUSINESS decision that lives in the
/// handler + domain (it requires a DB lookup), so we deliberately do not attempt it here.
/// </summary>
public sealed class RefreshTokenValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("Refresh token is required.");
    }
}
