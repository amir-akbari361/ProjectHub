using ProjectHub.Domain.Enums;

namespace ProjectHub.Application.Features.Tasks.GetTaskById;

/// <summary>
/// The read model returned for a single task. A flat transport DTO — deliberately NOT the ProjectTask
/// aggregate. On the READ side of CQRS we expose exactly the fields the client needs, without leaking
/// domain value objects (TaskTitle) or encapsulated collections. Keeping a dedicated record means the
/// domain can evolve internally as long as this projection is preserved.
/// </summary>
public sealed record TaskResponse(
    Guid Id,
    Guid ProjectId,
    string Title,
    string? Description,
    ProjectTaskStatus Status,
    TaskPriority Priority,
    Guid? AssigneeId,
    DateTime CreatedAtUtc);
