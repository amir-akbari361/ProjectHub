using ProjectHub.Application.Abstractions.Messaging;

namespace ProjectHub.Application.Features.Admin.Users.SetUserStatus;

/// <summary>
/// Admin-only command switching an account off (<c>IsActive = false</c>) so its holder can no longer sign in.
/// Existing sessions die at their next token refresh, because login and refresh both check the flag.
/// </summary>
/// <remarks>
/// WHY DEACTIVATE RATHER THAN DELETE?
/// Every project membership, comment, task assignment and audit row references this user. Deleting the row
/// would either cascade that history away or leave dangling references; deactivating revokes access while
/// keeping the record intact, which is also what the audit trail's integrity depends on.
/// </remarks>
public sealed record DeactivateUserCommand(Guid UserId) : ICommand;
