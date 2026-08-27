using ProjectHub.Application.Abstractions.Messaging;

namespace ProjectHub.Application.Features.Sprints.StartSprint;

/// <summary>
/// Command to move a sprint from Planned to Active. Carries only the sprint id — the actor is resolved
/// from the authenticated principal inside the handler (for both authorization and the audit stamp), and
/// the transition itself is the domain's concern (<c>Sprint.Start</c> guards that the sprint is Planned).
/// Returns a non-generic <see cref="ProjectHub.Application.Common.Result"/>: there is no payload, only
/// success or a modeled failure.
/// </summary>
public sealed record StartSprintCommand(Guid SprintId) : ICommand;
