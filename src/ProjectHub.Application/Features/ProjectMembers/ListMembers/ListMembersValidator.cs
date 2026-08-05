using FluentValidation;

namespace ProjectHub.Application.Features.ProjectMembers.ListMembers;

/// <summary>
/// Validates the SHAPE of a <see cref="ListMembersQuery"/>. The only structural precondition is that the
/// project id is present. VISIBILITY (caller is a member) is an authorization concern resolved in the
/// handler, not a shape rule.
/// </summary>
public sealed class ListMembersValidator : AbstractValidator<ListMembersQuery>
{
    public ListMembersValidator()
    {
        RuleFor(query => query.ProjectId)
            .NotEmpty();
    }
}
