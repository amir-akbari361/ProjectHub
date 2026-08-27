using ProjectHub.Application.Abstractions.Messaging;

namespace ProjectHub.Application.Features.Sprints.CreateSprint;

/// <summary>
/// Command to create a new sprint inside a project. The caller supplies the parent project id plus the
/// sprint's name and its schedule boundaries. The creator's identity is NOT part of the payload — it is
/// resolved from the authenticated principal via <c>ICurrentUser</c> inside the handler and used both for
/// authorization (must be a project member with a mutating role) and for the CreatedBy audit stamp.
/// </summary>
/// <remarks>
/// The schedule is carried as two plain <see cref="DateTime"/> values rather than a domain
/// <c>DateRange</c>: the Application layer accepts primitives at its boundary and builds the value object
/// itself (coercing the boundaries to UTC first, since <c>DateRange.Create</c> rejects any other kind).
/// </remarks>
public sealed record CreateSprintCommand(
    Guid ProjectId,
    string Name,
    DateTime StartUtc,
    DateTime EndUtc)
    : ICommand<CreateSprintResponse>;
