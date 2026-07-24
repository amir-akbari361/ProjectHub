using ProjectHub.Domain.Entities;
using ProjectHub.Domain.Events;
using ProjectHub.Domain.Exceptions;
using ProjectHub.Domain.ValueObjects;

namespace ProjectHub.Domain.Tests.Entities;

public class UserTests
{
    private static readonly DateTime UtcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static User RegisterUser() =>
        User.Register(
            Email.Create("jane.doe@example.com"),
            "Jane",
            "Doe",
            "hashed-password",
            UtcNow);

    [Fact]
    public void Register_ShouldCreateActiveUnconfirmedUser_AndRaiseEvent()
    {
        var user = RegisterUser();

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal("jane.doe@example.com", user.Email.Value);
        Assert.Equal("Jane Doe", user.FullName);
        Assert.True(user.IsActive);
        Assert.False(user.IsEmailConfirmed);
        Assert.Contains(user.DomainEvents, e => e is UserRegisteredDomainEvent);
    }

    [Fact]
    public void Register_ShouldThrow_WhenPasswordHashIsEmpty()
    {
        Assert.Throws<ArgumentException>(() =>
            User.Register(Email.Create("jane@example.com"), "Jane", "Doe", " ", UtcNow));
    }

    [Fact]
    public void AssignRole_ShouldAddRole_AndRaiseEvent()
    {
        var user = RegisterUser();
        user.ClearDomainEvents();

        user.AssignRole(Role.Member, UtcNow);

        Assert.Single(user.Roles);
        Assert.Contains(user.Roles, r => r.RoleId == Role.Member.Id);
        Assert.Contains(user.DomainEvents, e => e is UserRoleAssignedDomainEvent);
    }

    [Fact]
    public void AssignRole_ShouldThrow_WhenRoleAlreadyAssigned()
    {
        var user = RegisterUser();
        user.AssignRole(Role.Member, UtcNow);

        Assert.Throws<DomainException>(() => user.AssignRole(Role.Member, UtcNow));
    }

    [Fact]
    public void RemoveRole_ShouldRemoveExistingRole()
    {
        var user = RegisterUser();
        user.AssignRole(Role.Manager, UtcNow);

        user.RemoveRole(Role.Manager, UtcNow);

        Assert.Empty(user.Roles);
    }

    [Fact]
    public void RemoveRole_ShouldThrow_WhenRoleNotAssigned()
    {
        var user = RegisterUser();

        Assert.Throws<DomainException>(() => user.RemoveRole(Role.Admin, UtcNow));
    }

    [Fact]
    public void ConfirmEmail_ShouldSetConfirmedFlag()
    {
        var user = RegisterUser();

        user.ConfirmEmail(UtcNow);

        Assert.True(user.IsEmailConfirmed);
    }

    [Fact]
    public void ConfirmEmail_ShouldThrow_WhenAlreadyConfirmed()
    {
        var user = RegisterUser();
        user.ConfirmEmail(UtcNow);

        Assert.Throws<DomainException>(() => user.ConfirmEmail(UtcNow));
    }

    [Fact]
    public void Deactivate_ShouldThrow_WhenAlreadyInactive()
    {
        var user = RegisterUser();
        user.Deactivate(UtcNow);

        Assert.Throws<DomainException>(() => user.Deactivate(UtcNow));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing@domain")]
    [InlineData("@no-local.com")]
    public void Email_Create_ShouldThrow_WhenFormatIsInvalid(string invalid)
    {
        Assert.Throws<DomainException>(() => Email.Create(invalid));
    }

    [Fact]
    public void Email_Create_ShouldNormalizeToLowercaseTrimmed()
    {
        var email = Email.Create("  Jane.DOE@Example.COM  ");

        Assert.Equal("jane.doe@example.com", email.Value);
    }
}
