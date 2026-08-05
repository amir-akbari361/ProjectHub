using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Common;

namespace ProjectHub.Application.Features.Search.GlobalSearch;

/// <summary>
/// Cross-entity search over everything the CALLER can see: projects they belong to and tasks inside those
/// projects. A READ-side request returning a single paged, uniform stream of <see cref="SearchResultItem"/>.
/// The recipient/scope is NEVER a parameter — it is resolved from the token in the handler — so this query
/// carries only the search term and paging, keeping it impossible to search on another user's behalf.
/// </summary>
/// <remarks>
/// WHY PAGE A UNION?
/// The result set mixes two sources and can be large, so we return a <see cref="PagedList{T}"/> like the
/// project/task lists rather than an unbounded blob. Defaults mirror the other list queries so a bare call
/// is valid and predictable.
/// </remarks>
public sealed record GlobalSearchQuery(
    string SearchTerm,
    int PageNumber = 1,
    int PageSize = 20)
    : IQuery<PagedList<SearchResultItem>>;
