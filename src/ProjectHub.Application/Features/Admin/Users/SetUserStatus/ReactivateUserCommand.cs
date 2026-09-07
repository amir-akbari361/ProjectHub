using ProjectHub.Application.Abstractions.Messaging;

namespace ProjectHub.Application.Features.Admin.Users.SetUserStatus;

/// <summary>
/// Admin-only command restoring a deactivated account so its holder can sign in again. The mirror image of
/// <see cref="DeactivateUserCommand"/>.
/// </summary>
public sealed record ReactivateUserCommand(Guid UserId) : ICommand;
