using FluentValidation;
using MediatR;
using ProjectHub.Application.Abstractions.Messaging;
using ValidationException = ProjectHub.Application.Common.Exceptions.ValidationException;

namespace ProjectHub.Application.Behaviors;

/// <summary>
/// Runs every registered FluentValidation <see cref="IValidator{T}"/> for the incoming request.
/// Restricted to <see cref="IBaseCommand"/> — queries are read-only and typically require no
/// validation, and validating them would just add latency. Aggregates all failures and throws
/// a single <see cref="ValidationException"/> so callers see every problem at once, not one per round-trip.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IBaseCommand
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);

        var results = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = results
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToArray();

        if (failures.Length != 0)
        {
            throw new ValidationException(failures);
        }

        return await next();
    }
}
