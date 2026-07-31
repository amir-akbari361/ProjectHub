using ProjectHub.Application.Abstractions.Messaging;

namespace ProjectHub.Application.Features.Authentication.Login;

/// <summary>
/// Command to authenticate a user and issue a signed JWT access token plus a DB-backed refresh token.
/// This is the WRITE side: it mutates state by creating a new <see cref="RefreshToken"/> row, so it
/// is an <see cref="ICommand{TResponse}"/>, not a query. The raw password is validated against the
/// stored BCrypt hash; if successful, we mint both tokens and return them to the client.
/// </summary>
/// <param name="Email">The user's email (lookup key).</param>
/// <param name="Password">The raw password to verify against the stored hash.</param>
public sealed record LoginCommand(string Email, string Password) : ICommand<LoginResponse>;
