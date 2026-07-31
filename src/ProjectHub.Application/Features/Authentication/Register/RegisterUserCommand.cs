using ProjectHub.Application.Abstractions.Messaging;

namespace ProjectHub.Application.Features.Authentication.Register;

/// <summary>
/// Command to register a brand-new user account. This is the WRITE side of CQRS — it mutates state,
/// so it is an <see cref="ICommand{TResponse}"/> (not a query). It is an immutable record: a command
/// is a "request to change something", and once dispatched its inputs must never be mutated in flight.
/// It returns a <see cref="RegisterUserResponse"/> so the caller learns the new user's id without a
/// second round-trip.
/// </summary>
/// <param name="Email">Raw email as typed by the user; normalized/validated downstream by the Email value object.</param>
/// <param name="FirstName">Given name; trimmed and required.</param>
/// <param name="LastName">Family name; trimmed and required.</param>
/// <param name="Password">Plaintext password; hashed by <c>IPasswordHasher</c> and never persisted or logged.</param>
public sealed record RegisterUserCommand(
    string Email,
    string FirstName,
    string LastName,
    string Password) : ICommand<RegisterUserResponse>;
