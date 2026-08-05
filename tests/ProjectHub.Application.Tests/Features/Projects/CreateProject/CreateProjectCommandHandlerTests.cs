using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Application.Common;
using ProjectHub.Application.Features.Projects.CreateProject;
using ProjectHub.Domain.Entities;
using ProjectHub.Domain.Enums;

namespace ProjectHub.Application.Tests.Features.Projects.CreateProject;

/// <summary>
/// Unit tests for <see cref="CreateProjectCommandHandler"/>. Every collaborator (repository, current
/// user, clock, unit of work) is mocked so we exercise ONLY the handler's orchestration logic without
/// EF Core or a real principal. We assert both the happy path (project + owner persisted, correct
/// response) and the guard rails (no authenticated user short-circuits before any write).
/// </summary>
public sealed class CreateProjectCommandHandlerTests
{
    private readonly Mock<IRepository<Project>> _projectRepositoryMock;
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<CreateProjectCommandHandler>> _loggerMock;
    private readonly CreateProjectCommandHandler _handler;

    public CreateProjectCommandHandlerTests()
    {
        _projectRepositoryMock = new Mock<IRepository<Project>>();
        _currentUserMock = new Mock<ICurrentUser>();
        _dateTimeProviderMock = new Mock<IDateTimeProvider>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<CreateProjectCommandHandler>>();

        _handler = new CreateProjectCommandHandler(
            _projectRepositoryMock.Object,
            _currentUserMock.Object,
            _dateTimeProviderMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WhenUserIsAuthenticated_ShouldCreateProjectSuccessfully()
    {
        // Arrange
        var creatorId = Guid.NewGuid();
        var utcNow = new DateTime(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc);
        var command = new CreateProjectCommand("  Apollo  ", "Moon landing programme");

        _currentUserMock.Setup(x => x.UserId).Returns(creatorId);
        _dateTimeProviderMock.Setup(x => x.UtcNow).Returns(utcNow);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().NotBeEmpty();
        result.Value.Name.Should().Be("Apollo"); // ProjectName.Create trims surrounding whitespace

        _projectRepositoryMock.Verify(
            x => x.AddAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _unitOfWorkMock.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenUserIsAuthenticated_ShouldAddCreatorAsOwner()
    {
        // Arrange
        var creatorId = Guid.NewGuid();
        var utcNow = new DateTime(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc);
        var command = new CreateProjectCommand("Gemini", null);

        _currentUserMock.Setup(x => x.UserId).Returns(creatorId);
        _dateTimeProviderMock.Setup(x => x.UtcNow).Returns(utcNow);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert: the creator must be persisted as the project's sole Owner (satisfies the aggregate's
        // "a project must always have at least one owner" invariant from birth).
        result.IsSuccess.Should().BeTrue();
        _projectRepositoryMock.Verify(
            x => x.AddAsync(It.Is<Project>(p =>
                p.Members.Count == 1 &&
                p.Members.Single().UserId == creatorId &&
                p.Members.Single().Role == ProjectRole.Owner),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenUserIsNotAuthenticated_ShouldReturnUnauthorizedAndNotPersist()
    {
        // Arrange: no principal id available (endpoint reached without auth).
        var command = new CreateProjectCommand("Orion", null);
        _currentUserMock.Setup(x => x.UserId).Returns((Guid?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);

        _projectRepositoryMock.Verify(
            x => x.AddAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWorkMock.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldSourceTimestampFromDateTimeProvider()
    {
        // Arrange: the handler must obtain "now" from the injected provider, never the ambient clock.
        var creatorId = Guid.NewGuid();
        var command = new CreateProjectCommand("Voyager", null);

        _currentUserMock.Setup(x => x.UserId).Returns(creatorId);
        _dateTimeProviderMock.Setup(x => x.UtcNow)
            .Returns(new DateTime(2026, 12, 25, 18, 30, 0, DateTimeKind.Utc));
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _dateTimeProviderMock.Verify(x => x.UtcNow, Times.AtLeastOnce);
    }
}
