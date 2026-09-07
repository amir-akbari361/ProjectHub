using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace ProjectHub.API.Hubs;

/// <summary>
/// Tells SignalR which claim identifies the user behind a connection, so <c>Clients.User(id)</c> reaches
/// the right inbox. It reads <see cref="ClaimTypes.NameIdentifier"/> — the SAME claim
/// <c>ICurrentUser.UserId</c> reads — which is the entire point of having this class.
/// </summary>
/// <remarks>
/// WHY NOT JUST USE THE BUILT-IN PROVIDER?
/// <c>DefaultUserIdProvider</c> also reads <see cref="ClaimTypes.NameIdentifier"/>, so today the behaviour
/// is identical and this class is not strictly required. It exists because the correctness of every push
/// currently rests on an IMPLICIT three-link chain: JwtProvider emits <c>sub</c> → the JWT handler's
/// inbound claim mapping rewrites <c>sub</c> to <c>NameIdentifier</c> → the default provider happens to
/// read that claim. Setting <c>MapInboundClaims = false</c> (a common hardening step, and the natural thing
/// to do alongside the handler's explicit <c>NameClaimType = "sub"</c>) would break the middle link and
/// silently send every notification into the void — no exception, no log, just an inbox that never updates.
/// Stating the claim here makes the dependency explicit and reviewable, and pins the push target to the
/// same claim the Application layer authorizes on, so the two can never disagree.
///
/// WHY NORMALIZE THROUGH <see cref="Guid"/>?
/// The pusher addresses recipients with <c>Guid.ToString()</c> (lowercase, hyphenated "D" format) while the
/// value in the token is whatever the signer wrote. SignalR compares user ids as ORDINAL strings, so a
/// difference in casing or format would be a silent miss. Round-tripping both sides through
/// <see cref="Guid"/> guarantees one canonical form. A claim that isn't a Guid yields null, which SignalR
/// treats as "no user" — the connection simply receives no targeted pushes instead of hijacking another's.
/// </remarks>
internal sealed class SubjectUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        var value = connection.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(value, out var userId) ? userId.ToString() : null;
    }
}
