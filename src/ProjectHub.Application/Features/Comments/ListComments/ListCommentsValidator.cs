using FluentValidation;

namespace ProjectHub.Application.Features.Comments.ListComments;

/// <summary>
/// Validates the SHAPE of a <see cref="ListCommentsQuery"/>. Guards the parent task id and clamps the
/// paging inputs to safe bounds so a client can never request page 0, a negative page, or an unbounded
/// page size (which would let one request pull the entire thread and exhaust memory). Visibility of the
/// task is a BUSINESS concern checked in the handler.
/// </summary>
public sealed class ListCommentsValidator : AbstractValidator<ListCommentsQuery>
{
    // Upper bound on a single page. Mirrors the ceiling used by ListTasks for a consistent API contract.
    private const int MaxPageSize = 100;

    public ListCommentsValidator()
    {
        RuleFor(query => query.TaskId)
            .NotEmpty();

        RuleFor(query => query.PageNumber)
            .GreaterThanOrEqualTo(1);

        RuleFor(query => query.PageSize)
            .GreaterThanOrEqualTo(1)
            .LessThanOrEqualTo(MaxPageSize);
    }
}
