using ProjectHub.Application.Abstractions.Messaging;

namespace ProjectHub.Application.Features.Projects.GetProjectById;

/// <summary>
/// Query to fetch a single project by its id. This is the READ side of CQRS: unlike a command it
/// carries no intent to mutate, so it implements <see cref="IQuery{TResponse}"/> and flows through a
/// dedicated <c>IQueryHandler</c>. The only input the client controls is the project id (bound from
/// the route). The CALLER'S identity is NOT part of this payload — it is resolved from the
/// authenticated principal inside the handler so we can enforce "you may only read projects you belong
/// to" without trusting client-supplied claims.
/// </summary>
public sealed record GetProjectByIdQuery(Guid ProjectId) : IQuery<ProjectResponse>;
