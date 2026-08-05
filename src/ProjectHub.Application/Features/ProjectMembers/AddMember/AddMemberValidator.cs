using FluentValidation;

namespace ProjectHub.Application.Features.ProjectMembers.AddMember;

/// <summary>
/// Validates the SHAPE of an <see cref="AddMemberCommand"/> before the handler runs. Structural checks
/// only — presence of the ids and that the role is a defined enum value. All BUSINESS rules (does the
/// project exist, may the caller manage members, is the user already a member) live in the handler and
/// the domain, because they require I/O or aggregate state the validator has no access to.
/// </summary>
/// <remarks>
/// WHY <c>IsInEnum</c> ON THE ROLE?
/// A JSON body can carry any integer for an enum. Without this guard a value like <c>(ProjectRole)99</c>
/// would sail past model binding and only fail deep in the domain (or worse, persist). Screening it here
/// turns a garbage role into a clean 400 at the edge.
/// </remarks>
public sealed class AddMemberValidator : AbstractValidator<AddMemberCommand>
{
    public AddMemberValidator()
    {
        RuleFor(command => command.ProjectId)
            .NotEmpty();

        RuleFor(command => command.UserId)
            .NotEmpty();

        RuleFor(command => command.Role)
            .IsInEnum();
    }
}
