using FluentValidation;

namespace ProjectHub.Application.Features.Attachments.DeleteAttachment;

/// <summary>
/// Validates the SHAPE of a <see cref="DeleteAttachmentCommand"/>. The only structural fact is a non-empty
/// attachment id; who may delete it (uploader or a project Manager) is a business rule checked in the
/// handler.
/// </summary>
public sealed class DeleteAttachmentValidator : AbstractValidator<DeleteAttachmentCommand>
{
    public DeleteAttachmentValidator()
    {
        RuleFor(command => command.AttachmentId)
            .NotEmpty();
    }
}
