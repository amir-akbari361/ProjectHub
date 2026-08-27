using ProjectHub.Application.Abstractions.Messaging;

namespace ProjectHub.Application.Features.Sprints.ListSprints;

/// <summary>
/// Query for every sprint in a project, ordered by start date. Unlike <c>ListTasksQuery</c> this is NOT
/// paged: a project has a handful of sprints, so a single ordered list is simpler than page math and the
/// UI renders them all at once. Visibility is still scoped to the caller's membership inside the handler.
/// </summary>
public sealed record ListSprintsQuery(Guid ProjectId) : IQuery<IReadOnlyList<SprintResponse>>;
