using ProjectHub.Domain.Common;
using ProjectHub.Domain.Enums;
using ProjectHub.Domain.Events;
using ProjectHub.Domain.Exceptions;
using ProjectHub.Domain.Primitives;
using ProjectHub.Domain.ValueObjects;

namespace ProjectHub.Domain.Entities;

public sealed class Project : AggregateRoot
{
    private Project(Guid id, ProjectName name, string? description, ProjectStatus status)
        : base(id)
    {
        Name = name;
        Description = description;
        Status = status;
    }

    private Project()
        : base(Guid.Empty)
    {
        Name = null!;
    }

    public ProjectName Name { get; private set; }

    public string? Description { get; private set; }

    public ProjectStatus Status { get; private set; }

    public static Project Create(ProjectName name, string? description, DateTime utcNow, Guid? createdBy = null)
    {
        Guard.NotNull(name, nameof(name));

        var project = new Project(Guid.NewGuid(), name, description, ProjectStatus.Active);
        project.MarkCreated(utcNow, createdBy);
        project.RaiseDomainEvent(new ProjectCreatedDomainEvent(project.Id, name.Value, utcNow));

        return project;
    }

    public void Rename(ProjectName newName, DateTime utcNow, Guid? updatedBy = null)
    {
        Guard.NotNull(newName, nameof(newName));

        if (Status == ProjectStatus.Archived)
        {
            throw new DomainException("An archived project cannot be renamed.");
        }

        Name = newName;
        MarkUpdated(utcNow, updatedBy);
    }

    public void Archive(DateTime utcNow, Guid? updatedBy = null)
    {
        if (Status == ProjectStatus.Archived)
        {
            throw new DomainException("The project is already archived.");
        }

        Status = ProjectStatus.Archived;
        MarkUpdated(utcNow, updatedBy);
        RaiseDomainEvent(new ProjectArchivedDomainEvent(Id, utcNow));
    }
}