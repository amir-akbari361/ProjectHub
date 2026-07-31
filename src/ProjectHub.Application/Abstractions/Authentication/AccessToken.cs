namespace ProjectHub.Application.Abstractions.Authentication;

/// <summary>
/// The result of minting a signed JWT access token. This is a transport DTO — it carries the
/// already-encoded token string plus the absolute UTC expiry so the Application layer can hand both
/// back to the client without ever knowing HOW the token was signed (RSA, HMAC, etc.).
/// </summary>
/// <param name="Value">The compact, URL-safe encoded JWT ("header.payload.signature").</param>
/// <param name="ExpiresAtUtc">Absolute expiry so the client can proactively refresh before it lapses.</param>
public sealed record AccessToken(string Value, DateTime ExpiresAtUtc);
