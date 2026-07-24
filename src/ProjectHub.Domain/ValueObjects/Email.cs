using System.Text.RegularExpressions;
using ProjectHub.Domain.Common;
using ProjectHub.Domain.Exceptions;
using ProjectHub.Domain.Primitives;

namespace ProjectHub.Domain.ValueObjects;

public sealed class Email : ValueObject
{
    private const int MaxLength = 320;

    private static readonly Regex EmailPattern = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private Email(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Email Create(string value)
    {
        var normalized = Guard.NotNullOrWhiteSpace(value, nameof(value)).Trim().ToLowerInvariant();

        if (normalized.Length > MaxLength)
        {
            throw new DomainException($"Email cannot exceed {MaxLength} characters.");
        }

        if (!EmailPattern.IsMatch(normalized))
        {
            throw new DomainException("Email format is invalid.");
        }

        return new Email(normalized);
    }

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
