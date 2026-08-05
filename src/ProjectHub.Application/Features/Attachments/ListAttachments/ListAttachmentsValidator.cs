using FluentValidation;

namespace ProjectHub.Application.Features.Attachments.ListAttachments;

/// <summary>
/// Validates the SHAPE of a <see cref="ListAttachmentsQuery"/>. The only structural fact to guard is a
/// non-empty parent task id — the list is unpaged (see the query's rationale), so there are no paging
/// inputs to clamp. Whether the caller may SEE the task's attachments is a business rule checked in the
/// handler against project membership.
/// </summary>
public sealed class ListAttachmentsValidator : AbstractValidator<ListAttachmentsQuery>
{
    public ListAttachmentsValidator()
    {
        RuleFor(query => query.TaskId)
            .NotEmpty();
    }
}
