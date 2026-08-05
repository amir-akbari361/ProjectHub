namespace ProjectHub.Web.Client.Http;

/// <summary>
/// A tiny client-side result envelope. Every API call returns one of these so a page can render a
/// value on success or a message on failure WITHOUT throwing/catching for expected outcomes (a 404 or
/// a validation error is a normal branch, not an exception). This mirrors the server's railway-oriented
/// <c>Result</c> so both tiers reason about failure the same way.
/// </summary>
public sealed class ApiResult<T>
{
    private ApiResult(bool isSuccess, T? value, string? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Error { get; }

    public static ApiResult<T> Success(T value) => new(true, value, null);
    public static ApiResult<T> Failure(string error) => new(false, default, error);
}

/// <summary>Payload-less variant for commands that succeed with 204 No Content.</summary>
public sealed class ApiResult
{
    private ApiResult(bool isSuccess, string? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public string? Error { get; }

    public static ApiResult Success() => new(true, null);
    public static ApiResult Failure(string error) => new(false, error);
}
