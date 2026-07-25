namespace ProjectHub.Application.Common.Exceptions;

/// <summary>
/// Thrown when a required aggregate cannot be located by the given key.
/// Handlers should generally prefer returning <c>Result.Failure(Error.NotFound(...))</c>;
/// this exception exists for helper extension methods and defensive infrastructure code
/// (e.g., <c>FirstOrThrow</c> guards) that cannot conveniently produce a <c>Result</c>.
/// </summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string entityName, object key)
        : base($"Entity '{entityName}' with key '{key}' was not found.")
    {
        EntityName = entityName;
        Key = key;
    }

    public string EntityName { get; }

    public object Key { get; }
}
