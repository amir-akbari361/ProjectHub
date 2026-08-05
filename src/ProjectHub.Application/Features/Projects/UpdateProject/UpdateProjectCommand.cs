using ProjectHub.Application.Abstractions.Messaging;

namespace ProjectHub.Application.Features.Projects.UpdateProject;

/// <summary>
/// Command to update a project's mutable descriptive fields (its name and description). The
/// <see cref="ProjectId"/> is bound from the route, not the body — the resource being modified is
/// identified by the URL, while the body carries the new VALUES. As with every command, the caller's
/// identity is NOT part of the payload: it is resolved from the authenticated principal in the handler
/// so we can both attribute the change (UpdatedBy) and authorize it (role check) without trusting input.
/// </summary>
/// <remarks>
/// WHY IS THIS A SINGLE "UPDATE" COMMAND AND NOT SEPARATE "RENAME" + "CHANGE DESCRIPTION" COMMANDS?
/// The DOMAIN exposes those as two distinct behaviors (Rename, ChangeDescription) because each has its
/// own invariants. At the APPLICATION boundary, though, a typical "edit project" form submits both
/// fields together, so one command that orchestrates both domain calls in a single transaction matches
/// the real use case and keeps the two changes atomic. The command is an APPLICATION concern; it is
/// free to compose several domain operations.
/// </remarks>
public sealed record UpdateProjectCommand(Guid ProjectId, string Name, string? Description)
    : ICommand;
