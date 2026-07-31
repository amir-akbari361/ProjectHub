namespace ProjectHub.Application.Features.Authentication.Register;

/// <summary>
/// Payload returned after a successful registration. We deliberately return ONLY the surrogate id
/// and the normalized email — never the password hash, never the full <c>User</c> aggregate. This
/// is the whole point of a response DTO: it is the app's public contract, decoupled from the domain
/// model so internal changes to <c>User</c> cannot silently leak or break the API surface.
/// </summary>
/// <param name="UserId">The newly-created user's identity, useful for follow-up calls (e.g., resend confirmation).</param>
/// <param name="Email">The normalized email the domain stored, echoed back for client confirmation.</param>
public sealed record RegisterUserResponse(Guid UserId, string Email);
