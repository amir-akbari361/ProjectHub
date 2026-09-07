using FluentValidation;

namespace ProjectHub.Application.Features.Admin.Users.ListUsers;

/// <summary>
/// Validates <see cref="ListUsersQuery"/>. Only the paging window and the search term's length need guarding:
/// both filters are legitimately absent, and the handler treats a blank search term as "no filter".
/// </summary>
public sealed class ListUsersValidator : AbstractValidator<ListUsersQuery>
{
    public ListUsersValidator()
    {
        // A bound, not a requirement — the term is optional. The cap keeps a pathological input out of a
        // LIKE pattern; it is well above any real name or address.
        RuleFor(q => q.SearchTerm)
            .MaximumLength(200).WithMessage("Search term must be 200 characters or fewer.")
            .When(q => q.SearchTerm is not null);

        RuleFor(q => q.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("Page number must be at least 1.");

        RuleFor(q => q.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Page size must be between 1 and 100.");
    }
}
