namespace ProjectHub.Application.Features.Authentication.Login;

/// <summary>
/// The token pair returned after a successful login. We follow the OAuth2 dual-token pattern:
///  • A short-lived <see cref="AccessToken"/> (JWT) the client sends on every API call.
///  • A long-lived <see cref="RefreshToken"/> the client exchanges for a new access token when the
///    access token expires — without forcing the user to re-enter credentials.
/// Splitting the two limits the blast radius of a leaked access token (it expires in minutes) while
/// still giving a smooth session experience. The refresh token here is the RAW value; only its
/// SHA-256 hash is stored server-side, so a DB leak cannot be replayed.
/// </summary>
/// <param name="AccessToken">The signed JWT string sent as a Bearer credential.</param>
/// <param name="AccessTokenExpiresAtUtc">Absolute expiry so the client can refresh proactively.</param>
/// <param name="RefreshToken">The raw refresh token — shown to the client exactly once.</param>
/// <param name="RefreshTokenExpiresAtUtc">Absolute expiry of the refresh grant.</param>
public sealed record LoginResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc);
