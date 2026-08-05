using System.Net.Http.Json;
using ProjectHub.Domain.Enums;
using ProjectHub.Web.Client.Models;

namespace ProjectHub.Web.Client.Http;

/// <summary>
/// Typed HTTP client for project membership operations: listing members, adding, changing roles,
/// and removing. Backs the project's Members management page.
/// </summary>
public sealed class MembersApiClient
{
    private readonly HttpClient _http;

    public MembersApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<ApiResult<List<MemberItem>>> ListAsync(Guid projectId)
    {
        var response = await _http.GetAsync($"api/projects/{projectId}/members");
        return await response.ToResultAsync<List<MemberItem>>();
    }

    public async Task<ApiResult> AddAsync(Guid projectId, Guid userId, ProjectRole role)
    {
        var response = await _http.PostAsJsonAsync(
            $"api/projects/{projectId}/members",
            new AddMemberRequest(userId, role));
        return await response.ToResultAsync();
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
