using ProjectHub.Application.Abstractions.Messaging;

namespace ProjectHub.Application.Features.Projects.CreateProject;

/// <summary>
/// Command to create a new project. The caller supplies only the human-facing fields (name and an
/// optional description); the creator's identity is NOT part of the payload — it is resolved from
/// the authenticated principal via <c>ICurrentUser</c> inside the handler. This is a security rule:
/// trusting a client-supplied "createdBy" would let a caller forge ownership of a project.
/// </summary>
public sealed record CreateProjectCommand(string Name, string? Description)
    : ICommand<CreateProjectResponse>;
