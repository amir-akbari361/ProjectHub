using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Services;

namespace ProjectHub.Application.Behaviors;

/// <summary>
/// SLA guardrail. Times every request and logs a warning when handling exceeds
/// <see cref="SlowRequestThresholdMs"/>. Warnings are cheap and grep-able; they surface
/// N+1 queries and missing indexes long before a customer complains.
/// </summary>
public sealed class PerformanceBehavior<TRequest, TResponse>(
    ILogger<PerformanceBehavior<TRequest, TResponse>> logger,
    ICurrentUser currentUser)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private const int SlowRequestThresholdMs = 500;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();

        var response = await next();

        timer.Stop();

        var elapsedMs = timer.ElapsedMilliseconds;
        if (elapsedMs <= SlowRequestThresholdMs)
        {
            return response;
        }

        var requestName = typeof(TRequest).Name;
        var userId = currentUser.UserId?.ToString() ?? "anonymous";

        logger.LogWarning(
            "Long running request {RequestName} ({ElapsedMs} ms) for user {UserId} {@Request}",
            requestName, elapsedMs, userId, request);

        return response;
    }
}
