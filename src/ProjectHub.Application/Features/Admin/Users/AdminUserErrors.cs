using ProjectHub.Application.Common;

namespace ProjectHub.Application.Features.Admin.Users;

/// <summary>
/// Errors for the Admin user-administration features. Grouped in one place so every handler in the folder
/// answers the same situation with the same code and wording.
/// </summary>
/// <remarks>
/// WHY DO THESE SAY MORE THAN THE AUDIT-TRAIL ERRORS DO?
/// The audit read deliberately hides whether an entity exists, because the caller might not be entitled to
/// know. Here the caller has already been proven to be an Admin, so there is no existence to protect —
/// "no such user" and "that user already has this role" are exactly the answers an administrator needs to
/// understand what happened, and hiding them would only make the console harder to use.
///
/// WHY ARE THE SELF-TARGETING RULES CONFLICTS RATHER THAN FORBIDDEN?
/// The Admin IS authorised to perform these operations — the problem is the specific target, not the
/// permission. A 409 says "this request conflicts with the current state" which is precisely the case;
/// a 403 would wrongly suggest the account lacks the privilege.
/// </remarks>
public static class AdminUserErrors
{
    public static readonly Error Unauthenticated = Error.Unauthorized(
        "Admin.Unauthenticated",
        "You must be signed in to administer users.");

    public static readonly Error AdminOnly = Error.Forbidden(
        "Admin.AdminOnly",
        "Only administrators can administer users.");

    public static Error UserNotFound(Guid userId) =>
        Error.NotFound("Admin.UserNotFound", $"No user was found with id '{userId}'.");

    public static Error RoleNotFound(string roleName) =>
        Error.NotFound("Admin.RoleNotFound", $"No role named '{roleName}' exists.");

    public static readonly Error AlreadyActive = Error.Conflict(
        "Admin.UserAlreadyActive",
        "That account is already active.");

    public static readonly Error AlreadyInactive = Error.Conflict(
        "Admin.UserAlreadyInactive",
        "That account is already deactivated.");

    public static Error RoleAlreadyAssigned(string roleName) =>
        Error.Conflict("Admin.RoleAlreadyAssigned", $"That user already has the '{roleName}' role.");

    public static Error RoleNotAssigned(string roleName) =>
        Error.Conflict("Admin.RoleNotAssigned", $"That user does not have the '{roleName}' role.");

    /// <summary>
    /// Guards the "locked myself out" mistake: an Admin deactivating their own account would be signed out at
    /// their next token refresh with no way back in through the UI.
    /// </summary>
    public static readonly Error CannotDeactivateSelf = Error.Conflict(
        "Admin.CannotDeactivateSelf",
        "You cannot deactivate your own account.");

    /// <summary>
    /// The other half of the lockout guard: dropping your OWN Admin role would remove your access to this
    /// console. Another administrator can still do it, so the privilege is not permanent — it just cannot be
    /// surrendered by accident in a single click.
    /// </summary>
    public static readonly Error CannotRemoveOwnAdminRole = Error.Conflict(
        "Admin.CannotRemoveOwnAdminRole",
        "You cannot remove the Admin role from your own account.");
}
