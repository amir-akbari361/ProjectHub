namespace ProjectHub.Application.Features.Search.GlobalSearch;

/// <summary>
/// Discriminates what KIND of thing a search hit points at. An ENUM, not a magic string, so the client
/// can branch on a closed, reviewable set and each variant maps to a stable icon/route. The read side
/// unifies heterogeneous sources (projects, tasks) into ONE result stream, and this tag is how a caller
/// tells them apart without inspecting shape.
/// </summary>
public enum SearchResultType
{
    Project = 0,
    Task = 1
}

/// <summary>
/// A single, uniform hit in a global search. This is the classic READ-side "unified projection": we run
/// separate, index-friendly queries per source (projects, tasks) and normalize each row into the SAME
/// flat record so the UI can render a single mixed list. It is deliberately NOT a domain aggregate — it
/// carries only what a result row needs to display and to build a navigation link.
/// </summary>
/// <remarks>
/// WHY A COMMON SHAPE INSTEAD OF PER-TYPE RESULTS?
/// A search box shows ONE ranked list of mixed entities. A shared record lets the handler concatenate
/// heterogeneous sources into a single ordered page. <see cref="Type"/> keeps them distinguishable and
/// <see cref="ProjectId"/> is always present so the client can deep-link even to a task (which needs its
/// parent project for context). For a project hit, <see cref="ProjectId"/> equals <see cref="Id"/>.
/// </remarks>
public sealed record SearchResultItem(
    SearchResultType Type,
    Guid Id,
    Guid ProjectId,
    string Title,
    string? Description,
    DateTime CreatedAtUtc);
