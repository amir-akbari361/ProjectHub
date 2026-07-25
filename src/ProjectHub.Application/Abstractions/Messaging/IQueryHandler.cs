using MediatR;
using ProjectHub.Application.Common;

namespace ProjectHub.Application.Abstractions.Messaging;

/// <summary>
/// Handles a read-only <see cref="IQuery{TResponse}"/> and returns its projection wrapped in <see cref="Result{TResponse}"/>.
/// </summary>
public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse>;
