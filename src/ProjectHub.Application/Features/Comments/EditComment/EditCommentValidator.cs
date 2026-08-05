using FluentValidation;

namespace ProjectHub.Application.Features.Comments.EditComment;

/// <summary>
/// Validates the SHAPE of an <see cref="EditCommentCommand"/>. Identical structural rules to AddComment:
/// a non-empty comment id and a body within the domain's length bounds. Authorship (only the author may
/// edit) is a BUSINESS invariant enforced by the domain aggregate, not a shape concern for this validator.
/// </summary>
public sealed class EditCommentValidator : AbstractValidator<EditCommentCommand>
{
    private const int BodyMaxLength = 2000;

    public EditCommentValidator()
    {
        RuleFor(command => command.CommentId)
            .NotEmpty();

        RuleFor(command => command.Body)
            .NotEmpty()
            .MaximumLength(BodyMaxLength);
    }
}
