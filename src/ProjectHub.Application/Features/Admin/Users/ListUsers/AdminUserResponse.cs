namespace ProjectHub.Application.Features.Admin.Users.ListUsers;

/// <summary>
/// The READ-side shape of one account in the Admin user console: who they are, whether the account is
/// usable, and which global roles they hold.
/// </summary>
/// <remarks>
/// WHY ROLE NAMES RATHER THAN IDS?
/// Names are what the rest of the system authorises on — the JWT carries them, <c>[Authorize(Roles=...)]</c>
/// matches them, and the console's role editor sends them back. Returning ids would force the client to keep
/// its own id→name table in step with the database, which is the kind of duplication that silently rots.
///
/// WHY NO PASSWORD OR TOKEN STATE?
/// Nothing on this page can act on them, and an admin console is a high-value target: the response carries
/// only what the UI renders. <c>IsEmailConfirmed</c> is included because it explains why an otherwise active
/// account may still be unable to do everything.
/// </remarks>
public sealed record AdminUserResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    bool IsActive,
    bool IsEmailConfirmed,
    IReadOnlyCollection<string> Roles,
    DateTime CreatedAtUtc);
