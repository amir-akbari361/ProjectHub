using ProjectHub.Domain.Primitives;

namespace ProjectHub.Domain.Entities;

public sealed class UserRole : Entity
{
    internal UserRole(Guid userId, Guid roleId)
        : base(Guid.NewGuid())
    {
        UserId = userId;
        RoleId = roleId;
    }

    private UserRole()
        : base(Guid.Empty)
    {
    }

    public Guid UserId { get; private set; }

    public Guid RoleId { get; private set; }
}
