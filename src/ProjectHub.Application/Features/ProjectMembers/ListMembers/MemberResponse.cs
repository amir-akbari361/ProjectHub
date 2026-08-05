using ProjectHub.Domain.Enums;

namespace ProjectHub.Application.Features.ProjectMembers.ListMembers;

/// <summary>
/// READ-side DTO for one row of a project's member roster. A flat, serialization-friendly projection of a
/// <c>ProjectMember</c> enriched with the member's display fields (email / full name) joined from the
/// <c>User</c> aggregate — so the client can render "who is on this project" without a second call.
/// </summary>
/// <remarks>
/// WHY A DEDICATED DTO INSTEAD OF RETURNING <c>ProjectMember</c>?
/// The domain entity carries private setters, encapsulated behavior, and NO user profile fields. A DTO
/// lets EF project the exact columns we need across the member→user join, keeps the API contract stable
/// and decoupled from the aggregate, and never leaks change-tracked entities to the transport layer.
/// </remarks>
public sealed record MemberResponse(
    Guid UserId,
    string Email,
    string FullName,
    ProjectRole Role,
    DateTime JoinedAtUtc);
