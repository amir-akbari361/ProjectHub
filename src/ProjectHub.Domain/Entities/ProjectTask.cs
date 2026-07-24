using ProjectHub.Domain.Common;
using ProjectHub.Domain.Enums;
using ProjectHub.Domain.Events;
using ProjectHub.Domain.Exceptions;
using ProjectHub.Domain.Primitives;
using ProjectHub.Domain.ValueObjects;

namespace ProjectHub.Domain.Entities;

public sealed class ProjectTask : AggregateRoot
{
    private ProjectTask(
        Guid id,
        Guid projectId,
        TaskTitle title,
        string? description,
        ProjectTaskStatus status,
        TaskPriority priority)
        : base(id)
    {
        ProjectId = projectId;
        Title = title;
        Description = description;
        Status = status;
        Priority = priority;
    }

    private ProjectTask()
        : base(Guid.Empty)
    {
        Title = null!;
    }

    public Guid ProjectId { get; private set; }

    public TaskTitle Title { get; private set; }

    public string? Description { get; private set; }

    public ProjectTaskStatus Status { get; private set; }

    public TaskPriority Priority { get; private set; }

    public Guid? AssigneeId { get; private set; }

    public DateTime? DueDate { get; private set; }

    public static ProjectTask Create(
        Guid projectId,
        TaskTitle title,
        string? description,
        TaskPriority priority,
        DateTime utcNow,
        Guid? createdBy = null)
    {
        Guard.NotEmpty(projectId, nameof(projectId));
        Guard.NotNull(title, nameof(title));

        var task = new ProjectTask(
            Guid.NewGuid(),
            projectId,
            title,
            description,
            ProjectTaskStatus.Todo,
            priority);

        task.MarkCreated(utcNow, createdBy);
        task.RaiseDomainEvent(new TaskCreatedDomainEvent(task.Id, projectId, title.Value, utcNow));

        return task;
    }

    public void Assign(Guid assigneeId, DateTime utcNow, Guid? updatedBy = null)
    {
        Guard.NotEmpty(assigneeId, nameof(assigneeId));

        AssigneeId = assigneeId;
        MarkUpdated(utcNow, updatedBy);
        RaiseDomainEvent(new TaskAssignedDomainEvent(Id, ProjectId, assigneeId, utcNow));
    }

    public void ChangeStatus(ProjectTaskStatus newStatus, DateTime utcNow, Guid? updatedBy = null)
    {
        if (Status == newStatus)
        {
            throw new DomainException($"Task is already in status '{newStatus}'.");
        }

        var oldStatus = Status;
        Status = newStatus;
        MarkUpdated(utcNow, updatedBy);
        RaiseDomainEvent(new TaskStatusChangedDomainEvent(Id, ProjectId, oldStatus, newStatus, utcNow));
    }

    public void SetDueDate(DateTime dueDate, DateTime utcNow, Guid? updatedBy = null)
    {
        if (dueDate <= utcNow)
        {
            throw new DomainException("Due date must be in the future.");
        }

        DueDate = dueDate;
        MarkUpdated(utcNow, updatedBy);
    }

    public void UpdatePriority(TaskPriority priority, DateTime utcNow, Guid? updatedBy = null)
    {
        Priority = priority;
        MarkUpdated(utcNow, updatedBy);
    }
}
