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

    // Reference navigation to the assigned Role. It carries no new column — it maps onto the existing
    // RoleId FK — and exists so a loaded User can reach its role NAMES (e.g. "Admin"), not just their
    // ids. The token layer needs this: a JWT role claim must be the name so [Authorize(Roles="Admin")]
    // can match. EF populates it only when explicitly included (.ThenInclude(ur => ur.Role)); it is
    // null on a UserRole the domain has just created in memory, hence the null-forgiving initializer.
    public Role Role { get; private set; } = null!;
}
