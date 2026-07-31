using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Common;

namespace ProjectHub.Application.Features.Authentication.ConfirmEmail;

/// <summary>
/// Command to activate an account by redeeming the one-time confirmation token that was emailed at
/// registration. Returns a plain <see cref="Result"/> (no payload) — success simply means "the email
/// is now verified." We take ONLY the raw token: possession of the emailed link IS proof the caller
/// controls the address, so we resolve the user server-side from the token's hash rather than trusting
/// a client-supplied id.
/// </summary>
/// <param name="Token">The raw confirmation token from the emailed activation link.</param>
public sealed record ConfirmEmailCommand(string Token) : ICommand;
