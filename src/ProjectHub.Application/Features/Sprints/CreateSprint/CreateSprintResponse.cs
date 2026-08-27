namespace ProjectHub.Application.Features.Sprints.CreateSprint;

/// <summary>
/// The response returned after a sprint is created. Deliberately minimal — the id (so the client can
/// reference the new resource) and the normalized name. The full sprint projection is fetched via the
/// list endpoint; this keeps the create response an anti-corruption boundary that never leaks the whole
/// aggregate.
/// </summary>
public sealed record CreateSprintResponse(Guid Id, string Name);
