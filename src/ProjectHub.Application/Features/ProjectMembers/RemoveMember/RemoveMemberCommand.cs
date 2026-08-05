using ProjectHub.Application.Abstractions.Messaging;

namespace ProjectHub.Application.Features.ProjectMembers.RemoveMember;

/// <summary>
/// Command to remove an EXISTING member from a project. WRITE side of CQRS. Both ids come from the
/// ROUTE — they address the exact membership row to delete — while the CALLER is resolved from the
/// authenticated principal in the handler (never the payload) so the action can be authorized (role
/// check plus the Owner-only guard on removing an Owner) and attributed (UpdatedBy).
/// </summary>
public sealed record RemoveMemberCommand(
    Guid ProjectId,
    Guid UserId)
    : ICommand;
