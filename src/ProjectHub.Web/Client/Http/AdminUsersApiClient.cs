using System.Net.Http.Json;
using ProjectHub.Web.Client.Models;

namespace ProjectHub.Web.Client.Http;

/// <summary>
/// Typed HTTP client for the Admin user-administration endpoints: list accounts, switch them on and off, and
/// grant or revoke global roles.
/// </summary>
/// <remarks>
/// Every endpoint behind this client is gated by the API's <c>Admin</c> policy, so a non-admin gets a
/// <c>403</c> that surfaces here as a failed <see cref="ApiResult"/>. The client does NOT pre-check the
/// caller's role: the server is the authority, and duplicating the check here would only add a second place
/// for it to be wrong.
///
/// GLOBAL roles, not project roles. The names are the same strings the JWT carries and
/// <c>[Authorize(Roles=...)]</c> matches, which is why they travel as text rather than as an enum this project
/// would have to keep in step with the roles table.
/// </remarks>
public sealed class AdminUsersApiClient
{
    private readonly HttpClient _http;

    public AdminUsersApiClient(HttpClient http)
    {
        _http = http;
    }

    /// <summary>
    /// Returns one page of the account directory, alphabetically by email. <paramref name="isActive"/> is
    /// tri-state: null lists both active and deactivated accounts.
    /// </summary>
    public async Task<ApiResult<PagedResult<AdminUserItem>>> ListAsync(
        string? searchTerm = null,
        bool? isActive = null,
        int pageNumber = 1,
        int pageSize = 20)
    {
        var builder = new QueryStringBuilder()
            .Add("searchTerm", searchTerm);

        // Added only when set: the builder always emits a bool, and sending isActive=false when the user
        // asked for "all" would silently hide every active account.
        if (isActive is { } activeFilter)
        {
            builder.Add("isActive", activeFilter);
        }

        var query = builder
            .Add("pageNumber", pageNumber)
            .Add("pageSize", pageSize)
            .Build();

        var response = await _http.GetAsync($"api/admin/users{query}");
        return await response.ToResultAsync<PagedResult<AdminUserItem>>();
    }

    public async Task<ApiResult> DeactivateAsync(Guid userId)
    {
        var response = await _http.PostAsync($"api/admin/users/{userId}/deactivate", null);
        return await response.ToResultAsync();
    }

    public async Task<ApiResult> ActivateAsync(Guid userId)
    {
        var response = await _http.PostAsync($"api/admin/users/{userId}/activate", null);
        return await response.ToResultAsync();
    }

    public async Task<ApiResult> AssignRoleAsync(Guid userId, string roleName)
    {
        var response = await _http.PostAsJsonAsync(
            $"api/admin/users/{userId}/roles",
            new AssignRoleRequest(roleName));
        return await response.ToResultAsync();
    }

    /// <summary>
    /// Revokes a global role. The name is escaped into the path because it is a route segment — an unescaped
    /// value containing a slash would otherwise change which endpoint is addressed.
    /// </summary>
    public async Task<ApiResult> RemoveRoleAsync(Guid userId, string roleName)
    {
        var response = await _http.DeleteAsync(
            $"api/admin/users/{userId}/roles/{Uri.EscapeDataString(roleName)}");
        return await response.ToResultAsync();
    }
}
