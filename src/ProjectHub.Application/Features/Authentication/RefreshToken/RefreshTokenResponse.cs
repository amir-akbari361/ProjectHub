namespace ProjectHub.Application.Features.Authentication.RefreshToken;

/// <summary>
/// The new token pair returned after a successful refresh. Its shape is intentionally identical to
/// <see cref="Login.LoginResponse"/> because a refresh IS a re-authentication — the client treats
/// both responses the same way (store the new access token, replace the old refresh token).
///
/// We keep it as a SEPARATE type rather than reusing LoginResponse so the two endpoints can evolve
/// independently: if login later needs to add a "requiresMfa" flag, refresh shouldn't inherit it.
/// Duplicating four fields is cheaper than an accidental coupling between two security flows.
/// </summary>
/// <param name="AccessToken">The freshly-signed JWT.</param>
/// <param name="AccessTokenExpiresAtUtc">Absolute expiry of the new access token.</param>
/// <param name="RefreshToken">The NEW raw refresh token (the old one is now revoked — rotation).</param>
/// <param name="RefreshTokenExpiresAtUtc">Absolute expiry of the new refresh grant.</param>
public sealed record RefreshTokenResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc);
