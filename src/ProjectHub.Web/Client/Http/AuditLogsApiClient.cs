using ProjectHub.Web.Client.Models;

namespace ProjectHub.Web.Client.Http;

/// <summary>
/// Typed HTTP client for reading an entity's immutable audit trail.
/// </summary>
/// <remarks>
/// WHY THE ENTITY NAME IS AN ENUM AND NOT A FREE STRING
/// The API route is <c>api/auditlogs/{entityName}/{entityId}</c> and its validator rejects any name that is
/// not an audited type — so a typo ("Projects" instead of "Project") is a 400 at runtime. The API's audited
/// set is a closed list, which makes it exactly the sort of thing that belongs in the type system: passing
/// <see cref="AuditedEntity"/> turns that runtime rejection into a compile-time impossibility, and the enum
/// member name IS the wire value so there is no mapping table to keep in sync.
/// </remarks>
public sealed class AuditLogsApiClient
{
    private readonly HttpClient _http;

    public AuditLogsApiClient(HttpClient http)
    {
        _http = http;
    }

    /// <summary>
    /// Returns one page of an entity's audit trail, newest-first.
    /// </summary>
    public async Task<ApiResult<PagedResult<AuditLogItem>>> ListAsync(
        AuditedEntity entity,
        Guid entityId,
        int pageNumber = 1,
        int pageSize = 20)
    {
        var query = new QueryStringBuilder()
            .Add("pageNumber", pageNumber)
            .Add("pageSize", pageSize)
            .Build();

        var response = await _http.GetAsync($"api/auditlogs/{entity}/{entityId}{query}");
        return await response.ToResultAsync<PagedResult<AuditLogItem>>();
    }
}

/// <summary>
/// The entity types the API keeps an audit trail for. The member NAMES are the wire values, so this set must
/// mirror the server's <c>ListAuditLogsValidator</c> whitelist exactly.
/// </summary>
public enum AuditedEntity
{
    Project,
    ProjectTask,
    Sprint,
    ProjectMember,
    Comment,
    Attachment
}
