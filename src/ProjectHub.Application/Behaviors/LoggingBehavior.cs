using MediatR;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Services;

namespace ProjectHub.Application.Behaviors;

/// <summary>
/// Emits structured "handling" / "handled" logs around every MediatR request with the
/// authenticated user identifier for traceability. Placed after <c>UnhandledExceptionBehavior</c>
/// so exceptions are still captured, and before <c>ValidationBehavior</c> so we see the
/// invalid payload in logs when validation rejects a request.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger,
    ICurrentUser currentUser)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var userId = currentUser.UserId?.ToString() ?? "anonymous";

        logger.LogInformation(
            "Handling {RequestName} for user {UserId} {@Request}",
            requestName, userId, request);

        var response = await next();

        logger.LogInformation(
            "Handled {RequestName} for user {UserId}",
            requestName, userId);

        return response;
    }
}
