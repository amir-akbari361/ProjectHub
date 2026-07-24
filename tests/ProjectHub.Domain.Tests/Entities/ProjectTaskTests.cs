using ProjectHub.Domain.Entities;
using ProjectHub.Domain.Enums;
using ProjectHub.Domain.Events;
using ProjectHub.Domain.Exceptions;
using ProjectHub.Domain.ValueObjects;

namespace ProjectHub.Domain.Tests.Entities;

public class ProjectTaskTests
{
    private static readonly DateTime UtcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid ProjectId = Guid.NewGuid();

    private static ProjectTask CreateTask(string title = "Fix login bug") =>
        ProjectTask.Create(ProjectId, TaskTitle.Create(title), null, TaskPriority.Medium, UtcNow);

    [Fact]
    public void Create_ShouldReturnTodoTask_WithCorrectProperties()
    {
        var task = CreateTask();

        Assert.NotEqual(Guid.Empty, task.Id);
        Assert.Equal(ProjectId, task.ProjectId);
        Assert.Equal("Fix login bug", task.Title.Value);
        Assert.Equal(ProjectTaskStatus.Todo, task.Status);
        Assert.Equal(TaskPriority.Medium, task.Priority);
        Assert.Null(task.AssigneeId);
        Assert.Equal(UtcNow, task.CreatedAtUtc);
    }

    [Fact]
    public void Create_ShouldRaiseTaskCreatedDomainEvent()
    {
        var task = CreateTask();

        var domainEvent = Assert.Single(task.DomainEvents);
        var created = Assert.IsType<TaskCreatedDomainEvent>(domainEvent);
        Assert.Equal(task.Id, created.TaskId);
        Assert.Equal(ProjectId, created.ProjectId);
        Assert.Equal("Fix login bug", created.Title);
    }

    [Fact]
    public void Assign_ShouldSetAssigneeAndRaiseEvent()
    {
        var task = CreateTask();
        task.ClearDomainEvents();
        var assigneeId = Guid.NewGuid();

        task.Assign(assigneeId, UtcNow);

        Assert.Equal(assigneeId, task.AssigneeId);
        var domainEvent = Assert.Single(task.DomainEvents);
        var assigned = Assert.IsType<TaskAssignedDomainEvent>(domainEvent);
        Assert.Equal(assigneeId, assigned.AssigneeId);
    }

    [Fact]
    public void ChangeStatus_ShouldUpdateStatusAndRaiseEvent()
    {
        var task = CreateTask();
        task.ClearDomainEvents();

        task.ChangeStatus(ProjectTaskStatus.InProgress, UtcNow);

        Assert.Equal(ProjectTaskStatus.InProgress, task.Status);
        var domainEvent = Assert.Single(task.DomainEvents);
        var changed = Assert.IsType<TaskStatusChangedDomainEvent>(domainEvent);
        Assert.Equal(ProjectTaskStatus.Todo, changed.OldStatus);
        Assert.Equal(ProjectTaskStatus.InProgress, changed.NewStatus);
    }

    [Fact]
    public void ChangeStatus_ShouldThrow_WhenStatusIsSame()
    {
        var task = CreateTask();

        Assert.Throws<DomainException>(() => task.ChangeStatus(ProjectTaskStatus.Todo, UtcNow));
    }

    [Fact]
    public void SetDueDate_ShouldThrow_WhenDueDateIsInThePast()
    {
        var task = CreateTask();
        var pastDate = UtcNow.AddDays(-1);

        Assert.Throws<DomainException>(() => task.SetDueDate(pastDate, UtcNow));
    }

    [Fact]
    public void SetDueDate_ShouldSetDueDate_WhenDateIsInTheFuture()
    {
        var task = CreateTask();
        var futureDate = UtcNow.AddDays(7);

        task.SetDueDate(futureDate, UtcNow);

        Assert.Equal(futureDate, task.DueDate);
    }
}
