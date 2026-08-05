using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Domain.Enums;

namespace ProjectHub.Application.Features.ProjectMembers.ChangeMemberRole;

/// <summary>
/// Command to change the role of an EXISTING member of a project. WRITE side of CQRS. The
/// <see cref="ProjectId"/> and <see cref="UserId"/> both come from the ROUTE (they address the exact
/// membership row); <see cref="NewRole"/> comes from the body. The CALLER is resolved from the
/// authenticated principal in the handler — never from the payload — so the action can be authorized
/// (role check, plus the Owner-only guard on granting/revoking Owner) and attributed (UpdatedBy).
/// </summary>
public sealed record ChangeMemberRoleCommand(
    Guid ProjectId,
    Guid UserId,
    ProjectRole NewRole)
    : ICommand;
