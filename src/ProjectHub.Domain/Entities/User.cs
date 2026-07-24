using ProjectHub.Domain.Common;
using ProjectHub.Domain.Events;
using ProjectHub.Domain.Exceptions;
using ProjectHub.Domain.Primitives;
using ProjectHub.Domain.ValueObjects;

namespace ProjectHub.Domain.Entities;

public sealed class User : AggregateRoot
{
    private readonly List<UserRole> _roles = [];

    private User(Guid id, Email email, string firstName, string lastName, string passwordHash)
        : base(id)
    {
        Email = email;
        FirstName = firstName;
        LastName = lastName;
        PasswordHash = passwordHash;
        IsEmailConfirmed = false;
        IsActive = true;
    }

    private User()
        : base(Guid.Empty)
    {
        Email = null!;
        FirstName = null!;
        LastName = null!;
        PasswordHash = null!;
    }

    public Email Email { get; private set; }

    public string FirstName { get; private set; }

    public string LastName { get; private set; }

    public string PasswordHash { get; private set; }

    public bool IsEmailConfirmed { get; private set; }

    public bool IsActive { get; private set; }

    public string FullName => $"{FirstName} {LastName}";

    public IReadOnlyCollection<UserRole> Roles => _roles.AsReadOnly();

    public static User Register(
        Email email,
        string firstName,
        string lastName,
        string passwordHash,
        DateTime utcNow,
        Guid? createdBy = null)
    {
        Guard.NotNull(email, nameof(email));
        var normalizedFirstName = Guard.NotNullOrWhiteSpace(firstName, nameof(firstName)).Trim();
        var normalizedLastName = Guard.NotNullOrWhiteSpace(lastName, nameof(lastName)).Trim();
        var normalizedHash = Guard.NotNullOrWhiteSpace(passwordHash, nameof(passwordHash));

        var user = new User(Guid.NewGuid(), email, normalizedFirstName, normalizedLastName, normalizedHash);
        user.MarkCreated(utcNow, createdBy);
        user.RaiseDomainEvent(new UserRegisteredDomainEvent(user.Id, email.Value, utcNow));

        return user;
    }

    public void AssignRole(Role role, DateTime utcNow, Guid? updatedBy = null)
    {
        Guard.NotNull(role, nameof(role));

        if (_roles.Any(r => r.RoleId == role.Id))
        {
            throw new DomainException("The user already has this role.");
        }

        _roles.Add(new UserRole(Id, role.Id));
        MarkUpdated(utcNow, updatedBy);
        RaiseDomainEvent(new UserRoleAssignedDomainEvent(Id, role.Id, utcNow));
    }

    public void RemoveRole(Role role, DateTime utcNow, Guid? updatedBy = null)
    {
        Guard.NotNull(role, nameof(role));

        var existing = _roles.SingleOrDefault(r => r.RoleId == role.Id)
            ?? throw new DomainException("The user does not have this role.");

        _roles.Remove(existing);
        MarkUpdated(utcNow, updatedBy);
    }

    public void ConfirmEmail(DateTime utcNow, Guid? updatedBy = null)
    {
        if (IsEmailConfirmed)
        {
            throw new DomainException("The email is already confirmed.");
        }

        IsEmailConfirmed = true;
        MarkUpdated(utcNow, updatedBy);
    }

    public void ChangePassword(string newPasswordHash, DateTime utcNow, Guid? updatedBy = null)
    {
        PasswordHash = Guard.NotNullOrWhiteSpace(newPasswordHash, nameof(newPasswordHash));
        MarkUpdated(utcNow, updatedBy);
    }

    public void Deactivate(DateTime utcNow, Guid? updatedBy = null)
    {
        if (!IsActive)
        {
            throw new DomainException("The user is already inactive.");
        }

        IsActive = false;
        MarkUpdated(utcNow, updatedBy);
    }
}
