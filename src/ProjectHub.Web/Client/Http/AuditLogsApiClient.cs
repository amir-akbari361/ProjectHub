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

    /// <summary>
    /// Returns one page of the ORGANISATION-WIDE audit trail, newest-first. Admin-only: the API answers
    /// <c>403</c> for anyone else, which surfaces here as a failed <see cref="ApiResult{T}"/>.
    /// </summary>
    /// <remarks>
    /// Every filter is optional and omitted when null, so a bare call returns the most recent activity across
    /// all projects. <paramref name="entity"/> is the same closed enum the per-entity read uses, which keeps a
    /// mistyped entity name from reaching the server at all.
    ///
    /// <paramref name="toUtc"/> is compared inclusively by the API, so pass an END-OF-DAY value when the user
    /// picked a date rather than an instant — otherwise a filter of "to 27 August" excludes everything that
    /// happened during the 27th.
    /// </remarks>
    public async Task<ApiResult<PagedResult<AdminAuditLogItem>>> ListAllAsync(
        AuditedEntity? entity = null,
        Guid? projectId = null,
        Guid? performedBy = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        int pageNumber = 1,
        int pageSize = 20)
    {
        var query = new QueryStringBuilder()
            .Add("entityName", entity)
            .Add("projectId", projectId)
            .Add("performedBy", performedBy)
            .Add("fromUtc", fromUtc)
            .Add("toUtc", toUtc)
            .Add("pageNumber", pageNumber)
            .Add("pageSize", pageSize)
            .Build();

        var response = await _http.GetAsync($"api/admin/audit-logs{query}");
        return await response.ToResultAsync<PagedResult<AdminAuditLogItem>>();
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
