using MediatR;
using ProjectHub.Application.Common;

namespace ProjectHub.Application.Abstractions.Messaging;

/// <summary>
/// Marker interface for a read-only query. Queries never mutate state; separating them
/// from commands is the C in CQRS and enables read-side optimizations (e.g., caching,
/// projections, read-model DbContexts) without polluting the write path.
/// </summary>
public interface IQuery<TResponse> : IRequest<Result<TResponse>>;
