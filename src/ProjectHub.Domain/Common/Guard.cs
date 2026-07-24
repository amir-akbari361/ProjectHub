namespace ProjectHub.Domain.Common;

public static class Guard
{
    public static T NotNull<T>(T? value, string parameterName) where T : class
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);

        return value;
    }

    public static string NotNullOrWhiteSpace(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"'{parameterName}' cannot be null, empty, or whitespace.", parameterName);
        }

        return value;
    }

    public static Guid NotEmpty(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException($"'{parameterName}' cannot be empty.", parameterName);
        }

        return value;
    }

    public static int GreaterThanZero(int value, string parameterName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, $"'{parameterName}' must be greater than zero.");
        }

        return value;
    }
}