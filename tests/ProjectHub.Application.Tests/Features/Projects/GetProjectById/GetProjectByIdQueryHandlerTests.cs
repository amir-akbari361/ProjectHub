using FluentAssertions;
using Microsoft.Extensions.Logging;
using MockQueryable.Moq;
using Moq;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Application.Common;
using ProjectHub.Application.Features.Projects.GetProjectById;
using ProjectHub.Domain.Entities;
using ProjectHub.Domain.Enums;
using ProjectHub.Domain.ValueObjects;

namespace ProjectHub.Application.Tests.Features.Projects.GetProjectById;

/// <summary>
/// Unit tests for <see cref="GetProjectByIdQueryHandler"/>. Because this is a READ-side handler that
/// composes LINQ against <see cref="IApplicationDbContext"/>, we fake the <c>DbSet&lt;Project&gt;</c>
/// using MockQueryable.Moq — it turns an in-memory list into an IQueryable that supports the async EF
/// operators (SingleOrDefaultAsync) the handler calls. No real database is involved, so these remain
/// true unit tests focused on the handler's projection + authorization logic.
/// </summary>
public sealed class GetProjectByIdQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly Mock<ILogger<GetProjectByIdQueryHandler>> _loggerMock;
    private readonly GetProjectByIdQueryHandler _handler;

    private static readonly DateTime UtcNow = new(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc);

    public GetProjectByIdQueryHandlerTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _currentUserMock = new Mock<ICurrentUser>();
        _loggerMock = new Mock<ILogger<GetProjectByIdQueryHandler>>();

        _handler = new GetProjectByIdQueryHandler(
            _contextMock.Object,
            _currentUserMock.Object,
            _loggerMock.Object);
    }

    /// <summary>
    /// Builds a persisted-looking project with the given owner already added as a member, mirroring
    /// what CreateProject produces. Using the real aggregate (not a stub) keeps the projection under
    /// test honest — if ProjectName or the member collection changes shape, this test breaks first.
    /// </summary>
    private static Project CreateProjectWithOwner(Guid ownerId)
    {
        var project = Project.Create(ProjectName.Create("Apollo"), "Moon landing", UtcNow, ownerId);
        project.AddMember(ownerId, ProjectRole.Owner, UtcNow, ownerId);
        return project;
    }

    private void SetupProjects(params Project[] projects)
    {
        var mock = projects.AsQueryable().BuildMockDbSet();
        _contextMock.Setup(x => x.Projects).Returns(mock.Object);
    }

    [Fact]
    public async Task Handle_WhenProjectExistsAndCallerIsMember_ShouldReturnProjection()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var project = CreateProjectWithOwner(userId);
        SetupProjects(project);
        _currentUserMock.Setup(x => x.UserId).Returns(userId);

        // Act
        var result = await _handler.Handle(new GetProjectByIdQuery(project.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(project.Id);
        result.Value.Name.Should().Be("Apollo");
        result.Value.Description.Should().Be("Moon landing");
        result.Value.Status.Should().Be(ProjectStatus.Active);
        result.Value.Members.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new ProjectMemberResponse(userId, ProjectRole.Owner));
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotAMember_ShouldReturnNotFound()
    {
        // Arrange: project owned by SOMEONE ELSE; the caller is a valid authenticated user but not a
        // member. The membership filter must hide it and surface an indistinguishable 404.
        var ownerId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();
        var project = CreateProjectWithOwner(ownerId);
        SetupProjects(project);
        _currentUserMock.Setup(x => x.UserId).Returns(outsiderId);

        // Act
        var result = await _handler.Handle(new GetProjectByIdQuery(project.Id), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_WhenProjectDoesNotExist_ShouldReturnNotFound()
    {
        // Arrange: empty set — the id resolves to nothing.
        SetupProjects();
        _currentUserMock.Setup(x => x.UserId).Returns(Guid.NewGuid());

        // Act
        var result = await _handler.Handle(new GetProjectByIdQuery(Guid.NewGuid()), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_WhenUserIsNotAuthenticated_ShouldReturnUnauthorized()
    {
        // Arrange: no principal id. The handler must short-circuit BEFORE touching the DbSet.
        _currentUserMock.Setup(x => x.UserId).Returns((Guid?)null);

        // Act
        var result = await _handler.Handle(new GetProjectByIdQuery(Guid.NewGuid()), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
        _contextMock.Verify(x => x.Projects, Times.Never);
    }
}
