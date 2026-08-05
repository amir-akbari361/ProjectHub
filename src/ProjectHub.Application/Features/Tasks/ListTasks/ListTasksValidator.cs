using FluentValidation;

namespace ProjectHub.Application.Features.Tasks.ListTasks;

/// <summary>
/// Validates the pagination and filter inputs of a <see cref="ListTasksQuery"/>. Runs in the MediatR
/// ValidationBehavior before the handler, so an abusive request (huge page size, negative page) is
/// rejected with a 400 before any SQL is generated.
/// </summary>
public sealed class ListTasksValidator : AbstractValidator<ListTasksQuery>
{
    private const int MaxPageSize = 100;
    private const int SearchTermMaxLength = 200;

    public ListTasksValidator()
    {
        RuleFor(query => query.ProjectId)
            .NotEmpty();

        RuleFor(query => query.PageNumber)
            .GreaterThanOrEqualTo(1);

        RuleFor(query => query.PageSize)
            .GreaterThanOrEqualTo(1)
            .LessThanOrEqualTo(MaxPageSize);

        // Optional; only constrain length so a client cannot push an enormous string into the LIKE
        // pattern. MaximumLength treats null as valid, so no null guard is needed.
        RuleFor(query => query.SearchTerm)
            .MaximumLength(SearchTermMaxLength);

        RuleFor(query => query.SortBy)
            .IsInEnum();

        RuleFor(query => query.Status)
            .IsInEnum()
            .When(query => query.Status.HasValue);

        RuleFor(query => query.Priority)
            .IsInEnum()
            .When(query => query.Priority.HasValue);
    }
}
