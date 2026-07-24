using ProjectHub.Domain.Common;
using ProjectHub.Domain.Exceptions;
using ProjectHub.Domain.Primitives;

namespace ProjectHub.Domain.ValueObjects;

public sealed class TaskTitle : ValueObject
{
    private const int MaxLength = 500;

    private TaskTitle(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static TaskTitle Create(string value)
    {
        var normalized = Guard.NotNullOrWhiteSpace(value, nameof(value)).Trim();

        if (normalized.Length > MaxLength)
        {
            throw new DomainException($"Task title cannot exceed {MaxLength} characters.");
        }

        return new TaskTitle(normalized);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
