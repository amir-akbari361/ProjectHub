using MediatR;
using ProjectHub.Application.Common;

namespace ProjectHub.Application.Abstractions.Messaging;

/// <summary>
/// Marker interface for a command that mutates state and returns a <see cref="Result"/> with no payload.
/// Kept as a marker so pipeline behaviors can target commands specifically (as opposed to queries).
/// </summary>
public interface ICommand : IRequest<Result>, IBaseCommand;

/// <summary>
/// Marker interface for a command that mutates state and returns a typed payload wrapped in <see cref="Result{TResponse}"/>.
/// </summary>
public interface ICommand<TResponse> : IRequest<Result<TResponse>>, IBaseCommand;

/// <summary>
/// Non-generic root marker used by pipeline behaviors (e.g., transactions) that must recognize
/// any command regardless of its response type.
/// </summary>
public interface IBaseCommand;
