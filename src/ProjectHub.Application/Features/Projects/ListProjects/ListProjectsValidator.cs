using FluentValidation;

namespace ProjectHub.Application.Features.Projects.ListProjects;

/// <summary>
/// Validates the pagination and filter inputs of a <see cref="ListProjectsQuery"/>. Runs in the
/// MediatR <c>ValidationBehavior</c> before the handler, so an abusive request (page size of a million,
/// a negative page) is rejected with a 400 before any SQL is generated.
/// </summary>
/// <remarks>
/// WHY AN UPPER BOUND ON <see cref="ListProjectsQuery.PageSize"/> AND NOT JUST A LOWER ONE?
/// The page size is the single knob that controls how many rows we materialize. Without a ceiling a
/// client could request millions of rows and exhaust server memory — an availability (DoS) risk. The
/// cap makes the maximum cost of ANY single request bounded and predictable, which is a production
/// requirement, not a nicety.
/// </remarks>
public sealed class ListProjectsValidator : AbstractValidator<ListProjectsQuery>
{
    private const int MaxPageSize = 100;
    private const int SearchTermMaxLength = 200;

    public ListProjectsValidator()
    {
        RuleFor(query => query.PageNumber)
            .GreaterThanOrEqualTo(1);

        RuleFor(query => query.PageSize)
            .GreaterThanOrEqualTo(1)
            .LessThanOrEqualTo(MaxPageSize);

        // The search term is optional; only constrain its length so a client cannot push an enormous
        // string into the LIKE pattern. MaximumLength treats null as valid, so no null guard is needed.
        RuleFor(query => query.SearchTerm)
            .MaximumLength(SearchTermMaxLength);

        // Guard the enums against out-of-range integer values a client could post directly to the API.
        RuleFor(query => query.SortBy)
            .IsInEnum();

        RuleFor(query => query.Status)
            .IsInEnum()
            .When(query => query.Status.HasValue);
    }
}
