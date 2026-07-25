using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ProjectHub.Application.Common.Exceptions;

namespace ProjectHub.API.Infrastructure;

/// <summary>
/// The single, centralized translation point from thrown exceptions to RFC 7807 responses.
/// Implements <see cref="IExceptionHandler"/> — the .NET 8+ hook used by
/// <c>app.UseExceptionHandler()</c> — so we replace the older custom middleware pattern with the
/// framework-native abstraction. Every unhandled exception that escapes a MediatR handler funnels
/// here, gets mapped to a correct HTTP status, and is returned as a machine-readable
/// <see cref="ProblemDetails"/>. Controllers therefore contain zero try/catch noise.
/// </summary>
internal sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(
        IProblemDetailsService problemDetailsService,
        ILogger<GlobalExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    /// <summary>
    /// Returns <c>true</c> once it has fully written the response, telling the pipeline the
    /// exception is handled. Returning <c>false</c> would let the next handler (or the default
    /// 500 page) run — we always handle, so we always return true.
    /// </summary>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Log at Error only for the truly unexpected. Validation/NotFound/Conflict are expected,
        // client-driven outcomes; logging them as errors would drown real incidents in noise.
        var (statusCode, title) = MapException(exception);

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);
        }

        httpContext.Response.StatusCode = statusCode;

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Type = $"https://httpstatuses.io/{statusCode}"
        };

        // Surface per-field validation errors under the RFC 7807 "errors" extension so clients can
        // map them back onto form fields. We never leak stack traces or internal messages for 500s.
        if (exception is ValidationException validationException)
        {
            problemDetails.Extensions["errors"] = validationException.Errors;
        }

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails
        });
    }

    // One place mapping application exception types -> HTTP status. Adding a new exception type
    // means adding one arm here; nothing else in the API changes.
    private static (int StatusCode, string Title) MapException(Exception exception) => exception switch
    {
        ValidationException => (StatusCodes.Status400BadRequest, "One or more validation errors occurred."),
        NotFoundException => (StatusCodes.Status404NotFound, "The requested resource was not found."),
        ConflictException => (StatusCodes.Status409Conflict, "The request conflicts with the current state."),
        _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
    };
}
