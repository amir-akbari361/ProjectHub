using FluentValidation;

namespace ProjectHub.Application.Features.ProjectMembers.ChangeMemberRole;

/// <summary>
/// Validates the SHAPE of a <see cref="ChangeMemberRoleCommand"/>. Structural checks only — both ids are
/// present and the requested role is a defined enum value. BUSINESS rules (project/member exists, caller
/// may manage members, last-owner protection, Owner-only escalation) live in the handler and the domain,
/// because they need I/O or aggregate state the validator cannot see.
/// </summary>
public sealed class ChangeMemberRoleValidator : AbstractValidator<ChangeMemberRoleCommand>
{
    public ChangeMemberRoleValidator()
    {
        RuleFor(command => command.ProjectId)
            .NotEmpty();

        RuleFor(command => command.UserId)
            .NotEmpty();

        RuleFor(command => command.NewRole)
            .IsInEnum();
    }
}
