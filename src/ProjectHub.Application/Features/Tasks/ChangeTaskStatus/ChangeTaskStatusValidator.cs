using FluentValidation;

namespace ProjectHub.Application.Features.Tasks.ChangeTaskStatus;

/// <summary>
/// Validates the shape of a <see cref="ChangeTaskStatusCommand"/>. IsInEnum() rejects a status value
/// outside the defined set BEFORE the handler runs, so a client cannot slip an undefined enum integer
/// past the boundary. Whether the transition is allowed from the CURRENT status is a domain rule
/// enforced inside ProjectTask.ChangeStatus, not a shape concern.
/// </summary>
public sealed class ChangeTaskStatusValidator : AbstractValidator<ChangeTaskStatusCommand>
{
    public ChangeTaskStatusValidator()
    {
        RuleFor(command => command.TaskId)
            .NotEmpty();

        RuleFor(command => command.NewStatus)
            .IsInEnum();
    }
}
