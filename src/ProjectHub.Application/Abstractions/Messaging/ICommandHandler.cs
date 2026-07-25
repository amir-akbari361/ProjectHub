using MediatR;
using ProjectHub.Application.Common;

namespace ProjectHub.Application.Abstractions.Messaging;

/// <summary>
/// Handles a <see cref="ICommand"/> that returns a non-generic <see cref="Result"/>.
/// A dedicated handler alias keeps handler declarations short and communicates intent
/// (this is a command handler, not a generic MediatR request handler).
/// </summary>
public interface ICommandHandler<in TCommand> : IRequestHandler<TCommand, Result>
    where TCommand : ICommand;

/// <summary>
/// Handles a <see cref="ICommand{TResponse}"/> that returns a typed payload wrapped in a <see cref="Result{TResponse}"/>.
/// </summary>
public interface ICommandHandler<in TCommand, TResponse> : IRequestHandler<TCommand, Result<TResponse>>
    where TCommand : ICommand<TResponse>;
