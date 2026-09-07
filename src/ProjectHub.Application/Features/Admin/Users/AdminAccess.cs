using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Application.Common;

namespace ProjectHub.Application.Features.Admin.Users;

/// <summary>
/// Resolves the calling administrator, or explains why the call is refused. Every handler in this folder
/// starts with this, so the two-step "are you signed in / are you an Admin" check is written once and cannot
/// drift between operations.
/// </summary>
/// <remarks>
/// WHY A HELPER HERE WHEN THE CONTROLLER ALREADY CARRIES THE "Admin" POLICY?
/// Defence in depth. The policy protects one HTTP route; this protects the BEHAVIOUR, so a future caller —
/// another endpoint, a background job, a test — cannot reach these commands without the same check. Five
/// handlers repeating it by hand is five chances to write it slightly differently, or forget it entirely.
///
/// WHY RETURN THE ADMIN'S OWN ID?
/// Two of the operations need it: deactivation and role removal must refuse to target the caller's own
/// account. Returning it from the same call that authorises means a handler cannot use the identity without
/// having first passed the check.
///
/// WHY DISTINGUISH 401 FROM 403?
/// They have different remedies — "sign in" versus "this is not yours". Collapsing them would send a
/// signed-in non-admin to the login page, where signing in again changes nothing.
/// </remarks>
internal static class AdminAccess
{
    internal static Result<Guid> Resolve(ICurrentUser currentUser)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure<Guid>(AdminUserErrors.Unauthenticated);
        }

        return currentUser.IsAdmin
            ? userId
            : Result.Failure<Guid>(AdminUserErrors.AdminOnly);
    }
}
