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

    /// <summary>
    /// True when the caller holds the global "Admin" role. A convenience over <see cref="IsInRole"/>
    /// for the handlers that grant admins wider reach (the global audit viewer, user management) so the
    /// role name is not duplicated as a literal across the Application layer.
    /// </summary>
    bool IsAdmin { get; }
}
