using ProjectHub.Domain.Enums;

namespace ProjectHub.Application.Features.Projects.ListProjects;

/// <summary>
/// A single row in the project LIST projection. Deliberately leaner than the single-project
/// <c>ProjectResponse</c>: a list view renders a table, so it needs summary columns (name, status,
/// created date, a member count) but NOT the full member collection. Shaping a dedicated, minimal DTO
/// per read is the whole point of CQRS — we never over-fetch to satisfy a screen.
/// </summary>
/// <remarks>
/// WHY <see cref="MemberCount"/> AND NOT THE MEMBER LIST?
/// The grid only shows "how many people" per project. Projecting a COUNT lets EF translate it to a
/// correlated <c>COUNT(*)</c> subquery instead of loading and materializing every membership row for
/// every project on the page — an N+1 / over-fetch trap avoided by design.
/// </remarks>
public sealed record ProjectListItemResponse(
    Guid Id,
    string Name,
    string? Description,
    ProjectStatus Status,
    int MemberCount,
    DateTime CreatedAtUtc);
