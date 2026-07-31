using ProjectHub.Application.Abstractions.Messaging;

namespace ProjectHub.Application.Features.Authentication.RefreshToken;

/// <summary>
/// Command to exchange a still-valid refresh token for a NEW access/refresh token pair — the "silent
/// re-authentication" that keeps a user logged in without re-entering their password. This mutates
/// state (it rotates the old token and issues a new one), so it is an <see cref="ICommand{T}"/>.
///
/// We deliberately take ONLY the raw refresh token — not the user id. Trusting a client-supplied user
/// id would let an attacker present their own token while claiming someone else's id. Instead we
/// resolve the user FROM the token's hash server-side, so identity is proven by possession of the token.
/// </summary>
/// <param name="RefreshToken">The raw refresh token the client received at login (or last refresh).</param>
public sealed record RefreshTokenCommand(string RefreshToken) : ICommand<RefreshTokenResponse>;
