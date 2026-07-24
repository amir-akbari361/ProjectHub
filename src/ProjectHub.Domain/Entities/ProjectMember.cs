using ProjectHub.Domain.Enums;
using ProjectHub.Domain.Primitives;

namespace ProjectHub.Domain.Entities;

public sealed class ProjectMember : Entity
{
    internal ProjectMember(Guid projectId, Guid userId, ProjectRole role, DateTime utcNow)
        : base(Guid.NewGuid())
    {
        ProjectId = projectId;
        UserId = userId;
        Role = role;
        MarkCreated(utcNow);
    }

    private ProjectMember()
        : base(Guid.Empty)
    {
    }

    public Guid ProjectId { get; private set; }

    public Guid UserId { get; private set; }

    public ProjectRole Role { get; private set; }

    internal void ChangeRole(ProjectRole newRole, DateTime utcNow)
    {
        Role = newRole;
        MarkUpdated(utcNow);
    }
}
