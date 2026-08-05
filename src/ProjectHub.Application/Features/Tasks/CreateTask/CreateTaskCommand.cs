using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Domain.Enums;

namespace ProjectHub.Application.Features.Tasks.CreateTask;

/// <summary>
/// Command to create a new task inside a project. The caller supplies the parent project id plus the
/// human-facing fields (title, optional description, priority). The creator's identity is NOT part of
/// the payload — it is resolved from the authenticated principal via <c>ICurrentUser</c> inside the
/// handler and used both for authorization (must be a project member with a mutating role) and for the
/// CreatedBy audit stamp.
/// </summary>
public sealed record CreateTaskCommand(
    Guid ProjectId,
    string Title,
    string? Description,
    TaskPriority Priority)
    : ICommand<CreateTaskResponse>;
