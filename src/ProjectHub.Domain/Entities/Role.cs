using ProjectHub.Domain.Common;
using ProjectHub.Domain.Primitives;

namespace ProjectHub.Domain.Entities;

public sealed class Role : AggregateRoot
{
    public static readonly Role Admin = FromKnown("11111111-1111-1111-1111-111111111111", "Admin");
    public static readonly Role Manager = FromKnown("22222222-2222-2222-2222-222222222222", "Manager");
    public static readonly Role Member = FromKnown("33333333-3333-3333-3333-333333333333", "Member");

    private Role(Guid id, string name)
        : base(id)
    {
        Name = name;
    }

    private Role()
        : base(Guid.Empty)
    {
        Name = null!;
    }

    public string Name { get; private set; }

    public static Role Create(string name, DateTime utcNow, Guid? createdBy = null)
    {
        var normalizedName = Guard.NotNullOrWhiteSpace(name, nameof(name)).Trim();

        var role = new Role(Guid.NewGuid(), normalizedName);
        role.MarkCreated(utcNow, createdBy);

        return role;
    }

    private static Role FromKnown(string id, string name) => new(Guid.Parse(id), name);
}
