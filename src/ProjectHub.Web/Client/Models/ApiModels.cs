using ProjectHub.Domain.Enums;

namespace ProjectHub.Web.Client.Models;

// ---------------------------------------------------------------------------------------------------
// These are the WIRE contracts the Blazor client uses to talk to the API. They deliberately mirror the
// API's request/response shapes (not the domain entities). Keeping a dedicated client-side set means
// the UI never takes a hard dependency on server-internal records, and JSON (de)serialization has a
// concrete target with property names that match the API's camelCase output.
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

// ------------------------------- Comments -------------------------------

public sealed record AddCommentRequest(string Body);

public sealed record EditCommentRequest(string Body);

public sealed class AddCommentResult
{
    public Guid Id { get; set; }
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

public sealed class AttachmentItem
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeInBytes { get; set; }
    public Guid UploadedBy { get; set; }
    public DateTime CreatedAtUtc { get; set; }
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

public sealed class SearchResult
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Snippet { get; set; }
    public Guid? ProjectId { get; set; }
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
