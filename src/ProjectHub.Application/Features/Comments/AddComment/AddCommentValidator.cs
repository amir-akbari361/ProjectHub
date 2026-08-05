using FluentValidation;

namespace ProjectHub.Application.Features.Comments.AddComment;

/// <summary>
/// Validates the SHAPE of an <see cref="AddCommentCommand"/>. Runs in the MediatR ValidationBehavior
/// before the handler. It guards the two things that are purely structural — a non-empty task id and a
/// body within the domain's length bounds — so a malformed request is rejected with a 400 before any
/// database work happens. Whether the caller may actually comment on that task is a BUSINESS rule
/// verified against membership inside the handler, not here.
/// </summary>
public sealed class AddCommentValidator : AbstractValidator<AddCommentCommand>
{
    // Mirrors CommentBody.MaxLength in the domain. We duplicate the number deliberately: the validator
    // gives a friendly 400 with a precise message, while the value object remains the ultimate guard.
    private const int BodyMaxLength = 2000;

    public AddCommentValidator()
    {
        RuleFor(command => command.TaskId)
            .NotEmpty();

        RuleFor(command => command.Body)
            .NotEmpty()
            .MaximumLength(BodyMaxLength);
    }
}
