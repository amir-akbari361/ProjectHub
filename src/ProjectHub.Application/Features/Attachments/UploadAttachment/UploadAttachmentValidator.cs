using FluentValidation;

namespace ProjectHub.Application.Features.Attachments.UploadAttachment;

/// <summary>
/// Validates the SHAPE of an <see cref="UploadAttachmentCommand"/> before the handler runs. It guards the
/// purely structural facts we can check without touching the database or the byte stream: a real parent
/// task id, a non-empty file name/content type within the domain's bounds, a positive size that does not
/// exceed the domain cap, and a present stream factory. Whether the caller MAY upload to this task is a
/// business rule verified against project membership inside the handler.
/// </summary>
/// <remarks>
/// WHY DUPLICATE THE LIMITS FROM <c>FileMetadata</c>?
/// Same reasoning as every other feature: the validator produces a friendly 400 with a precise message
/// BEFORE we open the stream or hit storage, while <c>FileMetadata.Create</c> remains the ultimate guard
/// (defense in depth). If a caller lies about the size to slip past this rule, the domain still rejects it.
///
/// WHY VALIDATE THE DECLARED SIZE AT ALL IF THE CLIENT CAN LIE?
/// The declared <c>Content-Length</c> lets us reject an oversized upload cheaply, up front, without
/// streaming 100 MB just to discover it was too big. It is an optimization, not the security boundary —
/// the storage adapter must still enforce a hard byte ceiling while copying (belt and braces).
/// </remarks>
public sealed class UploadAttachmentValidator : AbstractValidator<UploadAttachmentCommand>
{
    // Mirror the domain constants in FileMetadata. Duplicated deliberately for a friendly early 400.
    private const int FileNameMaxLength = 260;
    private const int ContentTypeMaxLength = 200;
    private const long MaxSizeInBytes = 25 * 1024 * 1024; // 25 MB

    public UploadAttachmentValidator()
    {
        RuleFor(command => command.TaskId)
            .NotEmpty();

        RuleFor(command => command.FileName)
            .NotEmpty()
            .MaximumLength(FileNameMaxLength);

        RuleFor(command => command.ContentType)
            .NotEmpty()
            .MaximumLength(ContentTypeMaxLength);

        RuleFor(command => command.SizeInBytes)
            .GreaterThan(0)
            .LessThanOrEqualTo(MaxSizeInBytes);

        // The controller must always supply the deferred stream factory; a null here is a programming
        // error at the boundary, caught as a 400 rather than a NullReferenceException in the handler.
        RuleFor(command => command.OpenReadStream)
            .NotNull();
    }
}
