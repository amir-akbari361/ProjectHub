using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Domain.Enums;

namespace ProjectHub.Application.Features.Tasks.ChangeTaskStatus;

/// <summary>
/// Command to transition a task to a new workflow status (Todo → InProgress → InReview → Done, or any
/// move the board allows). WRITE side of CQRS. Carries the task id (from the route) and the target
/// status; the caller is resolved inside the handler for authorization and the UpdatedBy stamp.
/// </summary>
public sealed record ChangeTaskStatusCommand(
    Guid TaskId,
    ProjectTaskStatus NewStatus)
    : ICommand;
