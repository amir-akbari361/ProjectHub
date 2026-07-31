using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Common;

namespace ProjectHub.Application.Features.Authentication.RevokeToken;

/// <summary>
/// Command for LOGOUT — revokes the presented refresh token so it can never be exchanged again.
/// Returns a plain <see cref="Result"/> (no payload) because logout has nothing meaningful to hand
/// back; success simply means "this session is dead." We use <see cref="ICommand"/> (non-generic)
/// for exactly that reason — a command with no response value.
///
/// As with refresh, we take only the raw token, never a user id: possession of the token IS the
/// authorization to revoke it. This also keeps logout usable even after the access token expired.
/// </summary>
/// <param name="RefreshToken">The raw refresh token identifying the session to end.</param>
public sealed record RevokeTokenCommand(string RefreshToken) : ICommand;
