using ProjectHub.Domain.Entities;

namespace ProjectHub.Application.Abstractions.Authentication;

/// <summary>
/// Port for minting signed JWT access tokens. The Application layer depends on this abstraction;
/// the concrete signing strategy (RSA-256 asymmetric keys) lives in Infrastructure and is swapped
/// in at composition time. This is the Dependency Inversion Principle in action: the policy
/// (Application) owns the contract, the detail (Infrastructure) conforms to it.
/// </summary>
public interface IJwtProvider
{
    /// <summary>
    /// Builds a signed access token embedding the user's identity and role claims.
    /// </summary>
    /// <param name="user">The authenticated aggregate whose id, email, and roles become claims.</param>
    /// <returns>The encoded token plus its absolute UTC expiry.</returns>
    AccessToken GenerateAccessToken(User user);
}
