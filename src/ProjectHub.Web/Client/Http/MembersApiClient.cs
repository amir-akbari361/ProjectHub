using System.Net.Http.Json;
using ProjectHub.Domain.Enums;
using ProjectHub.Web.Client.Models;

namespace ProjectHub.Web.Client.Http;

/// <summary>
/// Typed HTTP client for a project's member roster: list, add, change role, remove.
/// </summary>
/// <remarks>
/// The whole controller is nested under a project (<c>api/projects/{projectId}/members</c>) because a
/// membership only exists in the context of one, and an individual membership is addressed by the USER's id
/// rather than a surrogate membership id — that is what a caller naturally holds when it says "change this
/// person's role".
/// </remarks>
public sealed class MembersApiClient
{
    private readonly HttpClient _http;

    public MembersApiClient(HttpClient http)
    {
        _http = http;
    }

    /// <summary>
    /// Lists the full roster. Deliberately un-paged, matching the API: a roster is a small bounded set, and
    /// the server enriches each row with the member's email and full name.
    /// </summary>
    public async Task<ApiResult<List<MemberItem>>> ListAsync(Guid projectId)
    {
        var response = await _http.GetAsync($"api/projects/{projectId}/members");
        return await response.ToResultAsync<List<MemberItem>>();
    }

    /// <summary>
    /// Adds a user to the project at an initial role. Returns the API's acknowledgement so a caller can tell a
    /// successful add from a no-op; the roster must still be re-listed to pick up the joined-in display fields
    /// the acknowledgement does not carry.
    /// </summary>
    public async Task<ApiResult<AddMemberResult>> AddAsync(Guid projectId, Guid userId, ProjectRole role)
    {
        var response = await _http.PostAsJsonAsync(
            $"api/projects/{projectId}/members",
            new AddMemberRequest(userId, role));
        return await response.ToResultAsync<AddMemberResult>();
    }

    public async Task<ApiResult> ChangeRoleAsync(Guid projectId, Guid userId, ProjectRole role)
    {
        var response = await _http.PutAsJsonAsync(
            $"api/projects/{projectId}/members/{userId}/role",
            new ChangeMemberRoleRequest(role));
        return await response.ToResultAsync();
    }

    public async Task<ApiResult> RemoveAsync(Guid projectId, Guid userId)
    {
        var response = await _http.DeleteAsync($"api/projects/{projectId}/members/{userId}");
        return await response.ToResultAsync();
    }
}
