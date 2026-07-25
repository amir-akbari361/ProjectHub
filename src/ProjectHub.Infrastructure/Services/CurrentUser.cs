using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ProjectHub.Application.Abstractions.Services;

namespace ProjectHub.Infrastructure.Services;

/// <summary>
/// The production implementation of <see cref="ICurrentUser"/>. It reads the authenticated principal
/// from the ambient <see cref="HttpContext"/> and projects it onto the host-agnostic abstraction the
/// Application layer consumes. Because it depends only on <see cref="IHttpContextAccessor"/>, the
/// Application/Domain layers never learn that HTTP exists — swap this class for a worker-service or
/// test double and every handler still compiles and runs unchanged.
/// </summary>
internal sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    // The principal is null outside a request (e.g. a background job) and null-conditional chaining
    // yields null rather than throwing — callers treat "no user" and "unauthenticated" uniformly.
    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            // "sub" (subject) is the OIDC/JWT standard claim for the user id; ASP.NET maps it to
            // ClaimTypes.NameIdentifier. TryParse guards against a malformed or missing claim so a
            // bad token degrades to "no user" instead of throwing deep inside a handler.
            var value = Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(value, out var userId) ? userId : null;
        }
    }

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    // Delegates to the framework's role evaluation, which already understands role claims and
    // ClaimsIdentity.RoleClaimType — we neither re-implement nor second-guess authorization here.
    public bool IsInRole(string role) => Principal?.IsInRole(role) ?? false;
}
