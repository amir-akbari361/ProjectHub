using FluentValidation;

namespace ProjectHub.Application.Features.ProjectMembers.RemoveMember;

/// <summary>
/// Validates the SHAPE of a <see cref="RemoveMemberCommand"/>. Structural checks only — both ids must be
/// present. BUSINESS rules (project/member exists, caller may manage members, Owner-only removal of an
/// Owner, last-owner protection) live in the handler and the domain, because they need I/O or aggregate
/// state the validator cannot see.
/// </summary>
public sealed class RemoveMemberValidator : AbstractValidator<RemoveMemberCommand>
{
    public RemoveMemberValidator()
    {
        RuleFor(command => command.ProjectId)
            .NotEmpty();

        RuleFor(command => command.UserId)
            .NotEmpty();
    }
}
