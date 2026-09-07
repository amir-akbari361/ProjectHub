using ProjectHub.Application.Abstractions.Messaging;

namespace ProjectHub.Application.Features.Admin.Users.ManageRoles;

/// <summary>
/// Admin-only command granting a global role (Admin / Manager / Member) to an account.
/// </summary>
/// <remarks>
/// WHY IS THE ROLE A NAME RATHER THAN AN ID?
/// The name is the value the whole system authorises on: the JWT carries it, <c>[Authorize(Roles=...)]</c>
/// matches it, and the console displays it. Taking the name means the client never has to know role ids, and
/// the handler resolves it against the roles table — so the database stays the single source of truth for
/// which roles exist.
///
/// WHEN DOES THE GRANT TAKE EFFECT?
/// Not immediately for a signed-in user: roles are baked into the access token at login, so the change is
/// visible once their token is next refreshed or they sign in again. That is a deliberate consequence of
/// stateless JWT authorisation, not an oversight.
/// </remarks>
public sealed record AssignUserRoleCommand(Guid UserId, string RoleName) : ICommand;
