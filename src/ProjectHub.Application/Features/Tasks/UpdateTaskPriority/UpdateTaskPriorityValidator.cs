using FluentValidation;

namespace ProjectHub.Application.Features.Tasks.UpdateTaskPriority;

/// <summary>
/// Validates the shape of an <see cref="UpdateTaskPriorityCommand"/>. IsInEnum() rejects a priority
/// value outside the defined set before the handler runs. Unlike ChangeStatus, priority has no
/// "no-op" rule — setting a task to its current priority is idempotent and harmless — so there is no
/// domain guard to translate.
/// </summary>
public sealed class UpdateTaskPriorityValidator : AbstractValidator<UpdateTaskPriorityCommand>
{
    public UpdateTaskPriorityValidator()
    {
        RuleFor(command => command.TaskId)
            .NotEmpty();

        RuleFor(command => command.Priority)
            .IsInEnum();
    }
}
