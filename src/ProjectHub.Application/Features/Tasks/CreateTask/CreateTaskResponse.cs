namespace ProjectHub.Application.Features.Tasks.CreateTask;

/// <summary>
/// The response returned after a task is created. Deliberately minimal — the id (so the client can
/// navigate to the new resource) and the normalized title. The full task projection is fetched via
/// GetTaskById; this keeps the create response an anti-corruption boundary that never leaks the whole
/// aggregate.
/// </summary>
public sealed record CreateTaskResponse(Guid Id, string Title);
