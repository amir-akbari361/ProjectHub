using MediatR;
using Microsoft.Extensions.Logging;

namespace ProjectHub.Application.Behaviors;

/// <summary>
/// Outermost pipeline behavior. Wraps the entire request pipeline in a try/catch,
/// logs any exception with the request payload for post-mortem analysis, and
/// re-throws so the API's exception middleware still sees the original exception.
/// This is a safety net; it does not swallow errors.
/// </summary>
public sealed class UnhandledExceptionBehavior<TRequest, TResponse>(
    ILogger<UnhandledExceptionBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        try
        {
            return await next();
        }
        catch (Exception ex)
        {
            var requestName = typeof(TRequest).Name;
            logger.LogError(ex,
                "Unhandled exception for request {RequestName} {@Request}",
                requestName, request);
            throw;
        }
    }
}
