using ProjectHub.Domain.Common;
using ProjectHub.Domain.Enums;
using ProjectHub.Domain.Events;
using ProjectHub.Domain.Exceptions;
using ProjectHub.Domain.Primitives;
using ProjectHub.Domain.ValueObjects;

namespace ProjectHub.Domain.Entities;

public sealed class Sprint : AggregateRoot
{
    private Sprint(Guid id, Guid projectId, string name, DateRange schedule, SprintStatus status)
        : base(id)
    {
        ProjectId = projectId;
        Name = name;
        Schedule = schedule;
        Status = status;
    }

    private Sprint()
        : base(Guid.Empty)
    {
        Name = null!;
        Schedule = null!;
    }

    public Guid ProjectId { get; private set; }

    public string Name { get; private set; }

    public DateRange Schedule { get; private set; }

    public SprintStatus Status { get; private set; }

    public static Sprint Create(
        Guid projectId,
        string name,
        DateRange schedule,
        DateTime utcNow,
        Guid? createdBy = null)
    {
        Guard.NotEmpty(projectId, nameof(projectId));
        var normalizedName = Guard.NotNullOrWhiteSpace(name, nameof(name)).Trim();
        Guard.NotNull(schedule, nameof(schedule));

        var sprint = new Sprint(Guid.NewGuid(), projectId, normalizedName, schedule, SprintStatus.Planned);
        sprint.MarkCreated(utcNow, createdBy);

        return sprint;
    }

    public void Start(DateTime utcNow, Guid? updatedBy = null)
    {
        if (Status != SprintStatus.Planned)
        {
            throw new DomainException("Only a planned sprint can be started.");
        }

        Status = SprintStatus.Active;
        MarkUpdated(utcNow, updatedBy);
        RaiseDomainEvent(new SprintStartedDomainEvent(Id, ProjectId, utcNow));
    }

    public void Complete(DateTime utcNow, Guid? updatedBy = null)
    {
        if (Status != SprintStatus.Active)
        {
            throw new DomainException("Only an active sprint can be completed.");
        }

        Status = SprintStatus.Completed;
        MarkUpdated(utcNow, updatedBy);
        RaiseDomainEvent(new SprintCompletedDomainEvent(Id, ProjectId, utcNow));
    }

    public void Reschedule(DateRange newSchedule, DateTime utcNow, Guid? updatedBy = null)
    {
        Guard.NotNull(newSchedule, nameof(newSchedule));

        if (Status == SprintStatus.Completed)
        {
            throw new DomainException("A completed sprint cannot be rescheduled.");
        }

        Schedule = newSchedule;
        MarkUpdated(utcNow, updatedBy);
    }
}
