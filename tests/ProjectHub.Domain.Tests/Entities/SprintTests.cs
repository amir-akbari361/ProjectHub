using ProjectHub.Domain.Entities;
using ProjectHub.Domain.Enums;
using ProjectHub.Domain.Events;
using ProjectHub.Domain.Exceptions;
using ProjectHub.Domain.ValueObjects;

namespace ProjectHub.Domain.Tests.Entities;

public class SprintTests
{
    private static readonly DateTime UtcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid ProjectId = Guid.NewGuid();

    private static DateRange TwoWeeks() =>
        DateRange.Create(UtcNow, UtcNow.AddDays(14));

    private static Sprint CreateSprint() =>
        Sprint.Create(ProjectId, "Sprint 1", TwoWeeks(), UtcNow);

    [Fact]
    public void Create_ShouldReturnPlannedSprint_WithCorrectProperties()
    {
        var sprint = CreateSprint();

        Assert.NotEqual(Guid.Empty, sprint.Id);
        Assert.Equal(ProjectId, sprint.ProjectId);
        Assert.Equal("Sprint 1", sprint.Name);
        Assert.Equal(SprintStatus.Planned, sprint.Status);
        Assert.Equal(14, sprint.Schedule.DurationInDays);
    }

    [Fact]
    public void Start_ShouldActivateSprint_AndRaiseEvent()
    {
        var sprint = CreateSprint();

        sprint.Start(UtcNow);

        Assert.Equal(SprintStatus.Active, sprint.Status);
        Assert.Contains(sprint.DomainEvents, e => e is SprintStartedDomainEvent);
    }

    [Fact]
    public void Start_ShouldThrow_WhenSprintIsNotPlanned()
    {
        var sprint = CreateSprint();
        sprint.Start(UtcNow);

        Assert.Throws<DomainException>(() => sprint.Start(UtcNow));
    }

    [Fact]
    public void Complete_ShouldCompleteActiveSprint_AndRaiseEvent()
    {
        var sprint = CreateSprint();
        sprint.Start(UtcNow);
        sprint.ClearDomainEvents();

        sprint.Complete(UtcNow);

        Assert.Equal(SprintStatus.Completed, sprint.Status);
        Assert.Contains(sprint.DomainEvents, e => e is SprintCompletedDomainEvent);
    }

    [Fact]
    public void Complete_ShouldThrow_WhenSprintIsNotActive()
    {
        var sprint = CreateSprint();

        Assert.Throws<DomainException>(() => sprint.Complete(UtcNow));
    }

    [Fact]
    public void Reschedule_ShouldThrow_WhenSprintIsCompleted()
    {
        var sprint = CreateSprint();
        sprint.Start(UtcNow);
        sprint.Complete(UtcNow);

        var newSchedule = DateRange.Create(UtcNow.AddDays(20), UtcNow.AddDays(34));

        Assert.Throws<DomainException>(() => sprint.Reschedule(newSchedule, UtcNow));
    }
}
