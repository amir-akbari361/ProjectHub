using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Common;
using ProjectHub.Domain.Enums;

namespace ProjectHub.Application.Features.Projects.ListProjects;

/// <summary>
/// Query to list the projects the CALLER belongs to, as a single page. This is the READ side of CQRS,
/// so it carries the three orthogonal concerns of every list endpoint — pagination, filtering, and
/// sorting — as explicit inputs rather than letting the client stream an entire table.
/// </summary>
/// <remarks>
/// WHY DEFAULTS ON THE RECORD AND NOT ONLY IN THE VALIDATOR?
/// A caller may omit page/size entirely (<c>GET /api/projects</c>). Defaulting here means the query is
/// always well-formed before the pipeline runs; the validator then CLAMPS the ranges (e.g., a page size
/// of 10 000) so a client cannot ask us to materialize an unbounded result set — a classic DoS vector.
///
/// WHY IS THE CALLER'S ID ABSENT FROM THE PAYLOAD?
/// Visibility is scoped to membership, and membership is derived from the authenticated principal inside
/// the handler — never from client input. Trusting a client-supplied "userId" would let a caller list
/// another user's projects.
/// </remarks>
public sealed record ListProjectsQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? SearchTerm = null,
    ProjectStatus? Status = null,
    ProjectSortBy SortBy = ProjectSortBy.CreatedAt,
    bool SortDescending = true)
    : IQuery<PagedList<ProjectListItemResponse>>;

/// <summary>
/// The whitelisted set of columns a client may sort by. Modeled as an ENUM rather than a free-text
/// "sortBy" string on purpose: it makes the sort surface a closed, reviewable contract and removes any
/// chance of a client steering an ORDER BY toward an unindexed or sensitive column.
/// </summary>
public enum ProjectSortBy
{
    Name = 0,
    CreatedAt = 1,
    Status = 2
}
