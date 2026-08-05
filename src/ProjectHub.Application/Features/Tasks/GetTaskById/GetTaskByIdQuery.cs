using ProjectHub.Application.Abstractions.Messaging;

namespace ProjectHub.Application.Features.Tasks.GetTaskById;

/// <summary>
/// Query to fetch a single task by its id. READ side of CQRS — no mutation intent, so it flows through
/// an <c>IQueryHandler</c>. The only client-controlled input is the task id (from the route). The
/// caller's identity is resolved from the authenticated principal inside the handler to enforce
/// "you may only read tasks in projects you belong to".
/// </summary>
public sealed record GetTaskByIdQuery(Guid TaskId) : IQuery<TaskResponse>;
