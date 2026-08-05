using System.Net.Http.Json;

namespace ProjectHub.Web.Client.Http;

/// <summary>
/// Shared helpers for turning an <see cref="HttpResponseMessage"/> into an <see cref="ApiResult{T}"/>.
/// Centralizing this here keeps every typed client DRY: they all parse success payloads and RFC 7807
/// problem details the same way, so error handling is consistent across the whole app.
/// </summary>
internal static class HttpResultExtensions
{
    public static async Task<ApiResult<T>> ToResultAsync<T>(this HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            return ApiResult<T>.Failure(await ExtractErrorAsync(response));
        }

        var value = await response.Content.ReadFromJsonAsync<T>();
        return value is not null
            ? ApiResult<T>.Success(value)
            : ApiResult<T>.Failure("Invalid response from server");
    }

    public static async Task<ApiResult> ToResultAsync(this HttpResponseMessage response)
    {
        return response.IsSuccessStatusCode
            ? ApiResult.Success()
            : ApiResult.Failure(await ExtractErrorAsync(response));
    }

    private static async Task<string> ExtractErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
            return problem?.Detail ?? response.ReasonPhrase ?? "Request failed";
        }
        catch
        {
            return response.ReasonPhrase ?? "Request failed";
        }
    }

    private sealed class ProblemDetailsDto
    {
        public string? Detail { get; set; }
        public string? Title { get; set; }
    }
}
