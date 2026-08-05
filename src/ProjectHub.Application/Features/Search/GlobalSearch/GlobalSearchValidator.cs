using FluentValidation;

namespace ProjectHub.Application.Features.Search.GlobalSearch;

/// <summary>
/// Validates the SHAPE of a <see cref="GlobalSearchQuery"/>. Guards the search term (non-empty, minimum
/// length, capped) so we never issue a wildcard LIKE over the whole table for a single character, and
/// clamps paging to a sane band. WHO may see WHICH rows is a business concern enforced in the handler by
/// scoping every query to the caller's memberships — never here.
/// </summary>
/// <remarks>
/// WHY A MINIMUM LENGTH?
/// A one-character "contains" search matches almost everything and forces an expensive full scan while
/// returning useless noise. Requiring at least two characters keeps the query selective and cheap.
/// </remarks>
public sealed class GlobalSearchValidator : AbstractValidator<GlobalSearchQuery>
{
    public GlobalSearchValidator()
    {
        RuleFor(query => query.SearchTerm)
            .NotEmpty()
            .MinimumLength(2)
            .MaximumLength(200);

        RuleFor(query => query.PageNumber)
            .GreaterThanOrEqualTo(1);

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100);
    }
}
