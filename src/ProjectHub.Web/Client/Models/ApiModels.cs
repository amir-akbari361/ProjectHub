 using ProjectHub.Application.Features.Search.GlobalSearch;
using ProjectHub.Domain.Enums;

namespace ProjectHub.Web.Client.Models;

// ---------------------------------------------------------------------------------------------------
// These are the WIRE contracts the Blazor client uses to talk to the API. They deliberately mirror the
// API's request/response shapes (not the domain entities). Keeping a dedicated client-side set means
// the UI never takes a hard dependency on server-internal records, and JSON (de)serialization has a
// concrete target with property names that match the API's camelCase output.
//
// ENUMS ARE THE ONE THING WE DO NOT REDECLARE. Domain enums (ProjectStatus, TaskPriority, …) and the
// Application's SearchResultType are reused directly: they ARE the contract, they are compiled into the
// Application assembly this project already references, and a hand-copied duplicate would eventually
// disagree with the server about a numeric value — the hardest class of bug to see, because the JSON
// still deserializes, just into the wrong member.
// ---------------------------------------------------------------------------------------------------

/// <summary>A single page of results plus paging metadata — mirrors the API's <c>PagedList&lt;T&gt;</c>.</summary>
public sealed class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public bool HasPreviousPage { get; set; }
    public bool HasNextPage { get; set; }
}

// ------------------------------- Authentication -------------------------------

public sealed record LoginRequest(string Email, string Password);

public sealed record RegisterRequest(string Email, string FirstName, string LastName, string Password);

public sealed record LoginResult(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc);

public sealed record RegisterResult(Guid UserId, string Email);

public sealed record RefreshRequest(string RefreshToken);

public sealed record RevokeRequest(string RefreshToken);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(string Token, string NewPassword);

// ------------------------------- Projects -------------------------------

public sealed record CreateProjectRequest(string Name, string? Description);

public sealed record UpdateProjectRequest(string Name, string? Description);

public sealed class ProjectListItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ProjectStatus Status { get; set; }
    public int MemberCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class ProjectDetail
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ProjectStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public List<ProjectMemberSummary> Members { get; set; } = new();
}

public sealed class ProjectMemberSummary
{
    public Guid UserId { get; set; }
    public ProjectRole Role { get; set; }
}

public sealed class CreateProjectResult
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

// ------------------------------- Members -------------------------------

public sealed record AddMemberRequest(Guid UserId, ProjectRole Role);

public sealed record ChangeMemberRoleRequest(ProjectRole Role);

/// <summary>
/// The acknowledgement returned by <c>POST /api/projects/{id}/members</c> — mirrors the API's
/// <c>AddMemberResponse</c>. Carries the surrogate membership id only; the roster is re-listed afterwards
/// because the response deliberately does not echo the enriched row (email, full name) that the list
/// projection joins in.
/// </summary>
public sealed class AddMemberResult
{
    public Guid MemberId { get; set; }
}

public sealed class MemberItem
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public ProjectRole Role { get; set; }
    public DateTime JoinedAtUtc { get; set; }
}

// ------------------------------- Tasks -------------------------------

public sealed record CreateTaskRequest(string Title, string? Description, TaskPriority Priority);

public sealed record AssignTaskRequest(Guid AssigneeId);

public sealed record ChangeTaskStatusRequest(ProjectTaskStatus NewStatus);

public sealed record UpdateTaskPriorityRequest(TaskPriority Priority);

public sealed class TaskItem
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ProjectTaskStatus Status { get; set; }
    public TaskPriority Priority { get; set; }
    public Guid? AssigneeId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class CreateTaskResult
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
}

// ------------------------------- Sprints -------------------------------

/// <summary>
/// Request body for creating a sprint — mirrors the API's <c>CreateSprintRequest</c>. The parent project
/// id is owned by the route, so it is NOT on the body. The two boundaries are plain <see cref="DateTime"/>s;
/// the server coerces them to UTC before building the domain <c>DateRange</c>.
/// </summary>
public sealed record CreateSprintRequest(string Name, DateTime StartUtc, DateTime EndUtc);

/// <summary>
/// One row in a project's sprint list — mirrors the API's <c>SprintResponse</c>. The owned schedule is
/// flattened to two boundaries, and <see cref="Status"/> is the domain <see cref="SprintStatus"/> enum
/// (reused, not redeclared): the API serializes it as a NUMBER, and a numeric JSON value binds natively to
/// the enum but would THROW into a string — the same trap documented for <see cref="SearchResult"/>.
/// </summary>
public sealed class SprintItem
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public SprintStatus Status { get; set; }
}

/// <summary>
/// The lean acknowledgement returned after a successful create — mirrors the API's
/// <c>CreateSprintResponse</c>. Carries just the id and normalized name; the UI re-lists to render the
/// full row (status, schedule) rather than trusting the create response to echo the whole aggregate.
/// </summary>
public sealed class CreateSprintResult
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

// ------------------------------- Comments -------------------------------

public sealed record AddCommentRequest(string Body);

public sealed record EditCommentRequest(string Body);

public sealed class AddCommentResult
{
    public Guid Id { get; set; }

    // Server-stamped creation time echoed back by the API's AddCommentResponse. Carrying it lets the UI
    // render the freshly posted comment optimistically (with a correct timestamp) without re-listing.
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class CommentItem
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public Guid AuthorId { get; set; }
    public string Body { get; set; } = string.Empty;
    public bool IsEdited { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}

// ------------------------------- Attachments -------------------------------

/// <summary>
/// One row in a task's attachment list. Mirrors the API's <c>AttachmentListItemResponse</c>: metadata
/// ONLY — never the bytes and never the server-side storage path (an implementation detail of the storage
/// adapter). To fetch the actual file the client calls the download endpoint with <see cref="Id"/>.
/// </summary>
public sealed class AttachmentItem
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeInBytes { get; set; }
    public Guid UploadedBy { get; set; }

    // Named to match the API's JSON field (uploadedAtUtc). The previous "CreatedAtUtc" silently stayed at
    // default(DateTime) because the property name never matched the wire contract — a classic quiet
    // deserialization bug that a dedicated wire model exists precisely to prevent.
    public DateTime UploadedAtUtc { get; set; }
}

/// <summary>
/// The lean write-side acknowledgement returned after a successful upload — mirrors the API's
/// <c>UploadAttachmentResponse</c>. Carries just enough (id, echoed file name, server-stamped time) for
/// the UI to render the new row optimistically without a second round-trip to re-list.
/// </summary>
public sealed class UploadAttachmentResult
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public DateTime UploadedAtUtc { get; set; }
}

// ------------------------------- Notifications -------------------------------

public sealed class NotificationItem
{
    public Guid Id { get; set; }
    public NotificationType Type { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReadAtUtc { get; set; }
}

// ------------------------------- Search -------------------------------

/// <summary>
/// One hit from the global search endpoint — mirrors the API's <c>SearchResultItem</c> exactly.
/// </summary>
/// <remarks>
/// THIS TYPE WAS PREVIOUSLY WRONG IN THREE WAYS, AND EACH IS WORTH RECORDING BECAUSE THEY ARE THE CLASSIC
/// FAILURE MODES OF A HAND-MAINTAINED WIRE MODEL:
///
/// 1. <c>Type</c> was declared <c>string</c>. The API serializes enums with the default
///    <c>System.Text.Json</c> settings, i.e. as NUMBERS, and reading a JSON number into a <c>string</c>
///    property THROWS rather than coercing — so every search request failed at deserialization. Declaring it
///    as the enum makes the numeric wire form bind natively.
/// 2. <c>Snippet</c> did not exist on the wire; the API sends <c>description</c>. A name that matches nothing
///    deserializes silently to null, so the UI just never showed the second line — no error, no clue.
/// 3. <c>ProjectId</c> was nullable. The API always populates it (a project hit carries its own id), so the
///    nullability forced every caller into a <c>HasValue</c> check that could never be false.
/// </remarks>
public sealed class SearchResult
{
    public SearchResultType Type { get; set; }
    public Guid Id { get; set; }

    /// <summary>
    /// The project this hit belongs to — for a Project hit, its own id. Always populated, which is what makes
    /// a single "open this result" navigation possible for both hit kinds.
    /// </summary>
    public Guid ProjectId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

// ------------------------------- Audit logs -------------------------------

public sealed class AuditLogItem
{
    public Guid Id { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public Guid? PerformedBy { get; set; }
    public string? Changes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>
/// One row of the organisation-wide (Admin) audit trail. A superset of <see cref="AuditLogItem"/>: the server
/// resolves the project name and performer email for this view, because a cross-project list in which every
/// actor and project is a bare GUID cannot be read.
/// </summary>
/// <remarks>
/// The resolved names are nullable on purpose — a row may have no project (the writer records it
/// best-effort), no performer (a system or seeder change), or reference a row that has since been
/// soft-deleted. Render a null as "unknown"/"system" rather than assuming a value.
/// </remarks>
public sealed class AdminAuditLogItem
{
    public Guid Id { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public Guid? PerformedBy { get; set; }
    public string? PerformedByEmail { get; set; }
    public string? Changes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

// ------------------------------- Admin: users -------------------------------

/// <summary>
/// One account in the Admin user directory. <see cref="Roles"/> holds GLOBAL role names (Admin/Manager/Member)
/// — not per-project roles, which are a separate concept carried by <see cref="MemberItem"/>.
/// </summary>
public sealed class AdminUserItem
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsEmailConfirmed { get; set; }
    public List<string> Roles { get; set; } = new();
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Display name, assembled client-side so the response carries no redundant field.</summary>
    public string FullName => $"{FirstName} {LastName}".Trim();
}

public sealed record AssignRoleRequest(string RoleName);
