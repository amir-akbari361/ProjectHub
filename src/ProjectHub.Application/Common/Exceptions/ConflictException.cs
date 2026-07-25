namespace ProjectHub.Application.Common.Exceptions;

/// <summary>
/// Thrown when an operation cannot complete because it conflicts with existing state
/// (e.g., a unique-index violation surfaced by the persistence layer). Prefer
/// <c>Result.Failure(Error.Conflict(...))</c> in handlers; this exists so the persistence
/// layer can translate <c>DbUpdateException</c> into a domain-shaped exception.
/// </summary>
public sealed class ConflictException(string message) : Exception(message);
