using ProjectHub.Domain.Common;
using ProjectHub.Domain.Exceptions;
using ProjectHub.Domain.Primitives;

namespace ProjectHub.Domain.ValueObjects;

public sealed class CommentBody : ValueObject
{
    private const int MaxLength = 2000;

    private CommentBody(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static CommentBody Create(string value)
    {
        var normalized = Guard.NotNullOrWhiteSpace(value, nameof(value)).Trim();

        if (normalized.Length > MaxLength)
        {
            throw new DomainException($"Comment cannot exceed {MaxLength} characters.");
        }

        return new CommentBody(normalized);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
