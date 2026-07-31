using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MockQueryable.Moq;
using Moq;
using ProjectHub.Application.Abstractions.Authentication;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Application.Features.Authentication;
using ProjectHub.Application.Features.Authentication.Register;
using ProjectHub.Domain.Entities;
using ProjectHub.Domain.ValueObjects;

namespace ProjectHub.Application.Tests.Features.Authentication.Register;

/// <summary>
/// Unit tests for <see cref="RegisterUserCommandHandler"/>. These tests verify the orchestration
/// logic WITHOUT hitting EF Core or BCrypt. Every port (IRepository, IPasswordHasher, etc.) is
/// a mock so we can control outcomes and assert interactions — the hallmark of a true unit test.
/// We use MockQueryable.Moq to fake DbSet LINQ calls like AnyAsync without spinning up InMemory.
/// </summary>
public sealed class RegisterUserCommandHandlerTests
{
    private readonly Mock<IRepository<User>> _userRepositoryMock;
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly Mock<IPasswordHasher> _passwordHasherMock;
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<RegisterUserCommandHandler>> _loggerMock;
    private readonly RegisterUserCommandHandler _handler;

    public RegisterUserCommandHandlerTests()
    {
        _userRepositoryMock = new Mock<IRepository<User>>();
        _contextMock = new Mock<IApplicationDbContext>();
        _passwordHasherMock = new Mock<IPasswordHasher>();
        _dateTimeProviderMock = new Mock<IDateTimeProvider>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<RegisterUserCommandHandler>>();

        _handler = new RegisterUserCommandHandler(
            _userRepositoryMock.Object,
            _contextMock.Object,
            _passwordHasherMock.Object,
            _dateTimeProviderMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WhenEmailIsUnique_ShouldRegisterUserSuccessfully()
    {
        // Arrange
        var command = new RegisterUserCommand(
            Email: "newuser@example.com",
            FirstName: "John",
            LastName: "Doe",
            Password: "SecurePass1");

        var utcNow = new DateTime(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc);
        var passwordHash = "hashed_SecurePass1";

        // Mock: no existing users with this email
        var emptyUserList = new List<User>().AsQueryable().BuildMockDbSet();
        _contextMock.Setup(x => x.Users).Returns(emptyUserList.Object);

        _passwordHasherMock.Setup(x => x.Hash(command.Password))
            .Returns(passwordHash);

        _dateTimeProviderMock.Setup(x => x.UtcNow)
            .Returns(utcNow);

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be("newuser@example.com"); // Email is normalized lowercase by Email.Create

        // Verify the password was hashed
        _passwordHasherMock.Verify(
            x => x.Hash(command.Password),
            Times.Once);

        // Verify the user was added to the repository
        _userRepositoryMock.Verify(
            x => x.AddAsync(It.Is<User>(u =>
                u.Email.Value == "newuser@example.com" &&
                u.FirstName == "John" &&
                u.LastName == "Doe" &&
                u.PasswordHash == passwordHash),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify SaveChangesAsync was called to persist the aggregate
        _unitOfWorkMock.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenEmailAlreadyExists_ShouldReturnConflictError()
    {
        // Arrange
        var command = new RegisterUserCommand(
            Email: "existing@example.com",
            FirstName: "John",
            LastName: "Doe",
            Password: "SecurePass1");

        var existingEmail = Email.Create("existing@example.com");
        var existingUser = User.Register(
            existingEmail,
            "Jane",
            "Smith",
            "some_hash",
            DateTime.UtcNow);

        // Mock: existing user with the same email
        var existingUserList = new List<User> { existingUser }.AsQueryable().BuildMockDbSet();
        _contextMock.Setup(x => x.Users).Returns(existingUserList.Object);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AuthErrors.EmailAlreadyInUse);

        // Verify that password hashing never happened (we short-circuit on conflict)
        _passwordHasherMock.Verify(
            x => x.Hash(It.IsAny<string>()),
            Times.Never);

        // Verify the repository was never called
        _userRepositoryMock.Verify(
            x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // Verify SaveChangesAsync was never called
        _unitOfWorkMock.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenEmailHasMixedCase_ShouldNormalizeToLowercase()
    {
        // Arrange: email with uppercase letters
        var command = new RegisterUserCommand(
            Email: "NewUser@EXAMPLE.COM",
            FirstName: "John",
            LastName: "Doe",
            Password: "SecurePass1");

        var utcNow = new DateTime(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc);
        var passwordHash = "hashed_SecurePass1";

        // Mock: no existing users
        var emptyUserList = new List<User>().AsQueryable().BuildMockDbSet();
        _contextMock.Setup(x => x.Users).Returns(emptyUserList.Object);

        _passwordHasherMock.Setup(x => x.Hash(command.Password))
            .Returns(passwordHash);

        _dateTimeProviderMock.Setup(x => x.UtcNow)
            .Returns(utcNow);

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // Email.Create normalizes to lowercase, so the response should echo back the normalized version
        result.Value.Email.Should().Be("newuser@example.com");

        // Verify the domain entity was created with the normalized email
        _userRepositoryMock.Verify(
            x => x.AddAsync(It.Is<User>(u => u.Email.Value == "newuser@example.com"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldSourceTimestampFromDateTimeProvider()
    {
        // Arrange: the handler must NEVER call DateTime.UtcNow directly — it must obtain "now" from
        // the injected IDateTimeProvider so time is controllable/deterministic in tests. We assert
        // the provider is the single source of truth by verifying it was consulted exactly once.
        var command = new RegisterUserCommand(
            Email: "timetest@example.com",
            FirstName: "John",
            LastName: "Doe",
            Password: "SecurePass1");

        var specificUtcTime = new DateTime(2026, 12, 25, 18, 30, 0, DateTimeKind.Utc);

        // Mock: no existing users
        var emptyUserList = new List<User>().AsQueryable().BuildMockDbSet();
        _contextMock.Setup(x => x.Users).Returns(emptyUserList.Object);

        _passwordHasherMock.Setup(x => x.Hash(It.IsAny<string>()))
            .Returns("hashed");

        _dateTimeProviderMock.Setup(x => x.UtcNow)
            .Returns(specificUtcTime);

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert: the abstraction was used (not the ambient system clock) and the user was persisted.
        _dateTimeProviderMock.Verify(x => x.UtcNow, Times.Once);
        _userRepositoryMock.Verify(
            x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldHashPasswordBeforeStoringUser()
    {
        // Arrange
        var command = new RegisterUserCommand(
            Email: "hashtest@example.com",
            FirstName: "John",
            LastName: "Doe",
            Password: "PlainTextPassword123");

        var expectedHash = "bcrypt_hash_of_PlainTextPassword123";

        // Mock: no existing users
        var emptyUserList = new List<User>().AsQueryable().BuildMockDbSet();
        _contextMock.Setup(x => x.Users).Returns(emptyUserList.Object);

        _passwordHasherMock.Setup(x => x.Hash(command.Password))
            .Returns(expectedHash);

        _dateTimeProviderMock.Setup(x => x.UtcNow)
            .Returns(DateTime.UtcNow);

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        // Verify the password was hashed
        _passwordHasherMock.Verify(
            x => x.Hash("PlainTextPassword123"),
            Times.Once);

        // Verify the user entity was created with the HASHED password, not the plaintext
        _userRepositoryMock.Verify(
            x => x.AddAsync(It.Is<User>(u => u.PasswordHash == expectedHash),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnUserIdInResponse()
    {
        // Arrange
        var command = new RegisterUserCommand(
            Email: "idtest@example.com",
            FirstName: "John",
            LastName: "Doe",
            Password: "SecurePass1");

        // Mock: no existing users
        var emptyUserList = new List<User>().AsQueryable().BuildMockDbSet();
        _contextMock.Setup(x => x.Users).Returns(emptyUserList.Object);

        _passwordHasherMock.Setup(x => x.Hash(It.IsAny<string>()))
            .Returns("hashed");

        _dateTimeProviderMock.Setup(x => x.UtcNow)
            .Returns(DateTime.UtcNow);

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().NotBeEmpty(); // GUID should be generated
        result.Value.Email.Should().Be("idtest@example.com");
    }
}
