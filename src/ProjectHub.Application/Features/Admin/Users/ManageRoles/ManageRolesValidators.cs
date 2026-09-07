using FluentValidation;

namespace ProjectHub.Application.Features.Admin.Users.ManageRoles;

/// <summary>
/// Shared rules for the two role commands: a target user and a non-blank role name.
/// </summary>
/// <remarks>
/// WHY IS THE ROLE NAME NOT CONSTRAINED TO AN ALLOW-LIST HERE?
/// Roles live in the database, so the authoritative list is a table — not a constant in this assembly. The
/// handler resolves the name against it and answers <c>Admin.RoleNotFound</c> for anything unknown, which
/// stays correct if a role is ever added. Validating against a hard-coded list would mean a newly seeded role
/// was rejected here before the handler ever got the chance to find it.
/// </remarks>
public sealed class AssignUserRoleValidator : AbstractValidator<AssignUserRoleCommand>
{
    public AssignUserRoleValidator()
    {
        RuleFor(c => c.UserId)
            .NotEmpty().WithMessage("User id is required.");

        RuleFor(c => c.RoleName)
            .NotEmpty().WithMessage("Role name is required.")
            .MaximumLength(100).WithMessage("Role name must be 100 characters or fewer.");
    }
}

/// <summary>Mirror of <see cref="AssignUserRoleValidator"/> for the removal command.</summary>
public sealed class RemoveUserRoleValidator : AbstractValidator<RemoveUserRoleCommand>
{
    public RemoveUserRoleValidator()
    {
        RuleFor(c => c.UserId)
            .NotEmpty().WithMessage("User id is required.");

        RuleFor(c => c.RoleName)
            .NotEmpty().WithMessage("Role name is required.")
            .MaximumLength(100).WithMessage("Role name must be 100 characters or fewer.");
    }
}
