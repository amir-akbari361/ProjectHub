namespace ProjectHub.Application.Abstractions.Authentication;

/// <summary>
/// Port for one-way password hashing and verification. The Application layer never stores or
/// compares raw passwords — it delegates to this abstraction so the hashing algorithm (BCrypt with
/// a per-hash salt and work factor) can evolve in Infrastructure without touching business logic.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Produces a salted, slow hash suitable for at-rest storage.</summary>
    string Hash(string password);

    /// <summary>
    /// Constant-time verification of a candidate password against a stored hash.
    /// Returns false rather than throwing so handlers can respond with a generic auth failure.
    /// </summary>
    bool Verify(string password, string passwordHash);
}
