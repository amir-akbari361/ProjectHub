using ProjectHub.Domain.Common;
using ProjectHub.Domain.Primitives;

namespace ProjectHub.Domain.ValueObjects;

public sealed class ProjectName : ValueObject
{
    private const int MaxLength = 200;

    private ProjectName(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static ProjectName Create(string value)
    {
        var normalizedValue = Guard.NotNullOrWhiteSpace(value, nameof(value)).Trim();

        if (normalizedValue.Length > MaxLength)
        {
            throw new Domain.Exceptions.DomainException($"Project name cannot exceed {MaxLength} characters.");
        }

        return new ProjectName(normalizedValue);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}