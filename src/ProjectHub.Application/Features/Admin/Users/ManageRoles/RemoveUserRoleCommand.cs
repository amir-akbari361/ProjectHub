using ProjectHub.Application.Abstractions.Messaging;

namespace ProjectHub.Application.Features.Admin.Users.ManageRoles;

/// <summary>
/// Admin-only command revoking a global role from an account. Refuses to remove the caller's OWN Admin role —
/// see <c>AdminUserErrors.CannotRemoveOwnAdminRole</c> for why.
/// </summary>
public sealed record RemoveUserRoleCommand(Guid UserId, string RoleName) : ICommand;
