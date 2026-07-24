using ProjectHub.Domain.Entities;
using ProjectHub.Domain.Enums;
using ProjectHub.Domain.Events;
using ProjectHub.Domain.Exceptions;
using ProjectHub.Domain.ValueObjects;

namespace ProjectHub.Domain.Tests.Entities;

public class ProjectMemberTests
{
    private static readonly DateTime UtcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static Project CreateProject()
    {
        var project = Project.Create(ProjectName.Create("Apollo"), null, UtcNow);
        project.ClearDomainEvents();
        return project;
    }

    [Fact]
    public void AddMember_ShouldAddMember_AndRaiseEvent()
    {
        var project = CreateProject();
        var userId = Guid.NewGuid();

        var member = project.AddMember(userId, ProjectRole.Contributor, UtcNow);

        Assert.Single(project.Members);
        Assert.Equal(userId, member.UserId);
        Assert.Equal(ProjectRole.Contributor, member.Role);
        Assert.Contains(project.DomainEvents, e => e is ProjectMemberAddedDomainEvent);
    }

    [Fact]
    public void AddMember_ShouldThrow_WhenUserAlreadyMember()
    {
        var project = CreateProject();
        var userId = Guid.NewGuid();
        project.AddMember(userId, ProjectRole.Viewer, UtcNow);

        Assert.Throws<DomainException>(() => project.AddMember(userId, ProjectRole.Owner, UtcNow));
    }

    [Fact]
    public void AddMember_ShouldThrow_WhenProjectArchived()
    {
        var project = CreateProject();
        project.Archive(UtcNow);

        Assert.Throws<DomainException>(() => project.AddMember(Guid.NewGuid(), ProjectRole.Viewer, UtcNow));
    }

    [Fact]
    public void ChangeMemberRole_ShouldUpdateRole_AndRaiseEvent()
    {
        var project = CreateProject();
        var userId = Guid.NewGuid();
        project.AddMember(userId, ProjectRole.Viewer, UtcNow);
        project.ClearDomainEvents();

        project.ChangeMemberRole(userId, ProjectRole.Maintainer, UtcNow);

        var member = Assert.Single(project.Members);
        Assert.Equal(ProjectRole.Maintainer, member.Role);
        Assert.Contains(project.DomainEvents, e => e is ProjectMemberRoleChangedDomainEvent);
    }

    [Fact]
    public void ChangeMemberRole_ShouldThrow_WhenMemberNotFound()
    {
        var project = CreateProject();

        Assert.Throws<DomainException>(() =>
            project.ChangeMemberRole(Guid.NewGuid(), ProjectRole.Owner, UtcNow));
    }

    [Fact]
    public void ChangeMemberRole_ShouldThrow_WhenDemotingLastOwner()
    {
        var project = CreateProject();
        var ownerId = Guid.NewGuid();
        project.AddMember(ownerId, ProjectRole.Owner, UtcNow);

        Assert.Throws<DomainException>(() =>
            project.ChangeMemberRole(ownerId, ProjectRole.Viewer, UtcNow));
    }

    [Fact]
    public void RemoveMember_ShouldRemove_AndRaiseEvent()
    {
        var project = CreateProject();
        var userId = Guid.NewGuid();
        project.AddMember(userId, ProjectRole.Contributor, UtcNow);
        project.ClearDomainEvents();

        project.RemoveMember(userId, UtcNow);

        Assert.Empty(project.Members);
        Assert.Contains(project.DomainEvents, e => e is ProjectMemberRemovedDomainEvent);
    }

    [Fact]
    public void RemoveMember_ShouldThrow_WhenRemovingLastOwner()
    {
        var project = CreateProject();
        var ownerId = Guid.NewGuid();
        project.AddMember(ownerId, ProjectRole.Owner, UtcNow);

        Assert.Throws<DomainException>(() => project.RemoveMember(ownerId, UtcNow));
    }

    [Fact]
    public void RemoveMember_ShouldAllowRemovingOwner_WhenAnotherOwnerExists()
    {
        var project = CreateProject();
        var firstOwner = Guid.NewGuid();
        var secondOwner = Guid.NewGuid();
        project.AddMember(firstOwner, ProjectRole.Owner, UtcNow);
        project.AddMember(secondOwner, ProjectRole.Owner, UtcNow);

        project.RemoveMember(firstOwner, UtcNow);

        Assert.Single(project.Members);
    }
}
