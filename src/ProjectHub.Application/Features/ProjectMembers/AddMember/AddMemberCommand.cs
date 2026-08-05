using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Domain.Enums;

namespace ProjectHub.Application.Features.ProjectMembers.AddMember;

/// <summary>
/// Command to add an existing user to a project with a given role. WRITE side of CQRS. The
/// <see cref="ProjectId"/> is bound from the ROUTE (the project being modified is identified by the URL);
/// the <see cref="UserId"/> and <see cref="Role"/> come from the request BODY (who to add, and as what).
/// The CALLER's identity is never part of the payload — it is resolved from the authenticated principal
/// in the handler so we can both authorize the action (role check) and attribute it (UpdatedBy).
/// </summary>
public sealed record AddMemberCommand(
    Guid ProjectId,
    Guid UserId,
    ProjectRole Role)
    : ICommand<AddMemberResponse>;
