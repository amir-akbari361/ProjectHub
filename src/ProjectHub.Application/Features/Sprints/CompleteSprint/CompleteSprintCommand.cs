using ProjectHub.Application.Abstractions.Messaging;

namespace ProjectHub.Application.Features.Sprints.CompleteSprint;

/// <summary>
/// Command to move a sprint from Active to Completed. Carries only the sprint id — the actor is resolved
/// from the authenticated principal inside the handler, and the transition is the domain's concern
/// (<c>Sprint.Complete</c> guards that the sprint is Active). Returns a non-generic
/// <see cref="ProjectHub.Application.Common.Result"/>: success or a modeled failure, no payload.
/// </summary>
public sealed record CompleteSprintCommand(Guid SprintId) : ICommand;
