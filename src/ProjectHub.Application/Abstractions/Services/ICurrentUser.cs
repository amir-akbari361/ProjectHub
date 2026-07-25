namespace ProjectHub.Application.Abstractions.Services;

/// <summary>
/// Abstraction over the authenticated principal so handlers can attribute changes
/// (CreatedBy, UpdatedBy, audit trail) without depending on ASP.NET Core's <c>HttpContext</c>.
/// The Infrastructure layer provides the implementation backed by <c>IHttpContextAccessor</c>.
/// </summary>
public interface ICurrentUser
{
    Guid? UserId { get; }

    bool IsAuthenticated { get; }

    bool IsInRole(string role);
}
