using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Application.Common;
using ProjectHub.Domain.Entities;
using ProjectHub.Domain.Enums;
using ProjectHub.Domain.ValueObjects;

namespace ProjectHub.Application.Features.Projects.CreateProject;

/// <summary>
/// Handles <see cref="CreateProjectCommand"/>. Like the auth handlers, this is a thin orchestrator:
/// it resolves the caller's identity, builds the <see cref="Project"/> aggregate through its domain
/// factory (which enforces every invariant and raises the ProjectCreatedDomainEvent), and commits
/// once via <see cref="IUnitOfWork"/>. The creator is added as the project's <see cref="ProjectRole.Owner"/>
/// in the SAME transaction, which is the only outcome consistent with the aggregate's existing rule
/// that "a project must always have at least one owner."
/// </summary>
public sealed class CreateProjectCommandHandler
    : ICommandHandler<CreateProjectCommand, CreateProjectResponse>
{
    private readonly IRepository<Project> _projectRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateProjectCommandHandler> _logger;

    public CreateProjectCommandHandler(
        IRepository<Project> projectRepository,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork,
        ILogger<CreateProjectCommandHandler> logger)
    {
        _projectRepository = projectRepository;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<CreateProjectResponse>> Handle(
        CreateProjectCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Resolve the authenticated caller. Creating a project requires a known user — they become
        //    the owner and the CreatedBy attribution. If the principal is missing a UserId, that means
        //    the endpoint was reached without authentication (a routing/authorization misconfiguration),
        //    so we fail fast rather than persist an ownerless project. This never trusts client input;
        //    the id comes from the validated JWT, not the command payload.
        if (_currentUser.UserId is not { } creatorId)
        {
            _logger.LogWarning("CreateProject reached the handler without an authenticated user.");
            return Result.Failure<CreateProjectResponse>(Error.Unauthorized(
                "Projects.Unauthenticated",
                "You must be signed in to create a project."));
        }

        // 2. Parse the name into its value object. ProjectName.Create trims and length-checks; the
        //    ValidationBehavior already rejected bad shapes with a 400, so reaching a throw here would
        //    indicate a validation gap (correctly surfaced as a 500 by UnhandledExceptionBehavior).
        var name = ProjectName.Create(request.Name);

        // 3. Build the aggregate through its factory. Project.Create sets status Active, stamps the
        //    audit fields via MarkCreated, and raises ProjectCreatedDomainEvent. The domain owns all
        //    of this — the handler only supplies the clock reading and the creator id.
        var utcNow = _dateTimeProvider.UtcNow;
        var project = Project.Create(name, request.Description, utcNow, creatorId);

        // 4. Auto-add the creator as Owner. This satisfies the aggregate invariant enforced elsewhere
        //    (a project cannot lose its last owner), so we establish that owner at birth. AddMember
        //    also raises ProjectMemberAddedDomainEvent, keeping the event stream complete.
        project.AddMember(creatorId, ProjectRole.Owner, utcNow, creatorId);

        // 5. Stage the insert and commit. One SaveChangesAsync flushes the project AND its member row
        //    atomically, then dispatches both domain events through PublishDomainEventsInterceptor.
        await _projectRepository.AddAsync(project, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Project {ProjectId} created by user {UserId} with name {ProjectName}",
            project.Id, creatorId, name.Value);

        // 6. Return the response DTO — the anti-corruption boundary. The API sees only id + normalized
        //    name, never the full Project aggregate or its member collection.
        return new CreateProjectResponse(project.Id, project.Name.Value);
    }
}
