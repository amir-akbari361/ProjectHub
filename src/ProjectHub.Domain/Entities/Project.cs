using ProjectHub.Domain.Common;
using ProjectHub.Domain.Enums;
using ProjectHub.Domain.Events;
using ProjectHub.Domain.Exceptions;
using ProjectHub.Domain.Primitives;
using ProjectHub.Domain.ValueObjects;

namespace ProjectHub.Domain.Entities;

public sealed class Project : AggregateRoot
{
    private readonly List<ProjectMember> _members = [];

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

    public IReadOnlyCollection<ProjectMember> Members => _members.AsReadOnly();


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

    public void ChangeDescription(string? newDescription, DateTime utcNow, Guid? updatedBy = null)
    {
        if (Status == ProjectStatus.Archived)
        {
            throw new DomainException("An archived project cannot be modified.");
        }

        // Normalize empty/whitespace to null so "" and "   " are stored identically to "no description".
        Description = string.IsNullOrWhiteSpace(newDescription) ? null : newDescription.Trim();
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

    public ProjectMember AddMember(Guid userId, ProjectRole role, DateTime utcNow, Guid? updatedBy = null)
    {
        Guard.NotEmpty(userId, nameof(userId));

        if (Status == ProjectStatus.Archived)
        {
            throw new DomainException("Members cannot be added to an archived project.");
        }

        if (_members.Any(m => m.UserId == userId))
        {
            throw new DomainException("The user is already a member of this project.");
        }

        var member = new ProjectMember(Id, userId, role, utcNow);
        _members.Add(member);
        MarkUpdated(utcNow, updatedBy);
        RaiseDomainEvent(new ProjectMemberAddedDomainEvent(Id, userId, role, utcNow));

        return member;
    }

    public void ChangeMemberRole(Guid userId, ProjectRole newRole, DateTime utcNow, Guid? updatedBy = null)
    {
        Guard.NotEmpty(userId, nameof(userId));

        var member = _members.SingleOrDefault(m => m.UserId == userId)
            ?? throw new DomainException("The user is not a member of this project.");

        if (member.Role == newRole)
        {
            throw new DomainException($"The member already has the role '{newRole}'.");
        }

        if (member.Role == ProjectRole.Owner && CountOwners() == 1)
        {
            throw new DomainException("A project must always have at least one owner.");
        }

        var oldRole = member.Role;
        member.ChangeRole(newRole, utcNow);
        MarkUpdated(utcNow, updatedBy);
        RaiseDomainEvent(new ProjectMemberRoleChangedDomainEvent(Id, userId, oldRole, newRole, utcNow));
    }

    public void RemoveMember(Guid userId, DateTime utcNow, Guid? updatedBy = null)
    {
        Guard.NotEmpty(userId, nameof(userId));

        var member = _members.SingleOrDefault(m => m.UserId == userId)
            ?? throw new DomainException("The user is not a member of this project.");

        if (member.Role == ProjectRole.Owner && CountOwners() == 1)
        {
            throw new DomainException("The last owner of a project cannot be removed.");
        }

        _members.Remove(member);
        MarkUpdated(utcNow, updatedBy);
        RaiseDomainEvent(new ProjectMemberRemovedDomainEvent(Id, userId, utcNow));
    }

    private int CountOwners() => _members.Count(m => m.Role == ProjectRole.Owner);
}


