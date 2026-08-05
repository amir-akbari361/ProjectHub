using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Domain.Enums;

namespace ProjectHub.Application.Features.Tasks.UpdateTaskPriority;

/// <summary>
/// Command to change a task's priority (Low → Medium → High → Critical). WRITE side of CQRS. Carries the
/// task id (from the route) and the target priority; the caller is resolved inside the handler for
/// authorization and the UpdatedBy stamp.
/// </summary>
public sealed record UpdateTaskPriorityCommand(
    Guid TaskId,
    TaskPriority Priority)
    : ICommand;
