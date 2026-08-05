using ProjectHub.Application.Abstractions.Messaging;

namespace ProjectHub.Application.Features.Tasks.AssignTask;

/// <summary>
/// Command to (re)assign a task to a project member. Carries the task id (from the route) plus the id
/// of the member who should own it. WRITE side of CQRS. The caller's identity is resolved inside the
/// handler for authorization and the UpdatedBy audit stamp — it is never part of the payload.
/// </summary>
public sealed record AssignTaskCommand(
    Guid TaskId,
    Guid AssigneeId)
    : ICommand;
