using ProjectHub.Domain.Entities;
using ProjectHub.Domain.Enums;
using ProjectHub.Domain.Events;
using ProjectHub.Domain.Exceptions;
using ProjectHub.Domain.ValueObjects;

namespace ProjectHub.Domain.Tests.Entities;

public class ProjectTests
{
    private static readonly DateTime UtcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_ShouldReturnActiveProject_WhenNameIsValid()
    {
        var name = ProjectName.Create("Apollo");

        var project = Project.Create(name, "Space program", UtcNow);

        Assert.NotEqual(Guid.Empty, project.Id);
        Assert.Equal(ProjectStatus.Active, project.Status);
        Assert.Equal("Apollo", project.Name.Value);
        Assert.Equal(UtcNow, project.CreatedAtUtc);
    }

    [Fact]
    public void Create_ShouldRaiseProjectCreatedDomainEvent()
    {
        var name = ProjectName.Create("Apollo");

        var project = Project.Create(name, null, UtcNow);

        var domainEvent = Assert.Single(project.DomainEvents);
        var created = Assert.IsType<ProjectCreatedDomainEvent>(domainEvent);
        Assert.Equal(project.Id, created.ProjectId);
        Assert.Equal("Apollo", created.Name);
    }

    [Fact]
    public void Archive_ShouldSetStatusToArchived_AndRaiseEvent()
    {
        var project = Project.Create(ProjectName.Create("Apollo"), null, UtcNow);
        project.ClearDomainEvents();

        project.Archive(UtcNow);

        Assert.Equal(ProjectStatus.Archived, project.Status);
        Assert.Contains(project.DomainEvents, e => e is ProjectArchivedDomainEvent);
    }

    [Fact]
    public void Archive_ShouldThrow_WhenAlreadyArchived()
    {
        var project = Project.Create(ProjectName.Create("Apollo"), null, UtcNow);
        project.Archive(UtcNow);

        Assert.Throws<DomainException>(() => project.Archive(UtcNow));
    }

    [Fact]
    public void Rename_ShouldThrow_WhenProjectIsArchived()
    {
        var project = Project.Create(ProjectName.Create("Apollo"), null, UtcNow);
        project.Archive(UtcNow);

        Assert.Throws<DomainException>(() => project.Rename(ProjectName.Create("Gemini"), UtcNow));
    }
}
