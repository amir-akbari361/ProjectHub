using ProjectHub.Domain.Enums;

namespace ProjectHub.Application.Features.Projects.GetProjectById;

/// <summary>
/// The read model returned for a single project. This is a flat, transport-shaped DTO — deliberately
/// NOT the <c>Project</c> aggregate. On the READ side of CQRS we are free to expose exactly the fields
/// a client needs, in the shape they need, without leaking domain value objects (<c>ProjectName</c>),
/// encapsulated collections, or behavior. Returning a dedicated record keeps the API contract stable:
/// the domain can evolve internally without breaking clients as long as this projection is preserved.
/// </summary>
/// <remarks>
/// WHY EXPOSE <see cref="Status"/> AS THE ENUM AND NOT A STRING?
/// The enum serializes to its name by our JSON options and gives strongly-typed clients an exact
/// contract. WHY INCLUDE THE MEMBER LIST? A project detail screen almost always renders "who's on this
/// project", so returning members here avoids an immediate second round-trip (a read-side optimization
/// that would be a code smell on the write side, but is exactly right for a query projection).
/// </remarks>
public sealed record ProjectResponse(
    Guid Id,
    string Name,
    string? Description,
    ProjectStatus Status,
    DateTime CreatedAtUtc,
    IReadOnlyList<ProjectMemberResponse> Members);

/// <summary>
/// A single membership row inside <see cref="ProjectResponse"/>. Flattened to the caller's user id and
/// role — the transport contract for "who belongs to this project and at what access level".
/// </summary>
public sealed record ProjectMemberResponse(Guid UserId, ProjectRole Role);
