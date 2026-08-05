using FluentValidation;

namespace ProjectHub.Application.Features.Tasks.AssignTask;

/// <summary>
/// Validates the shape of an <see cref="AssignTaskCommand"/>. Runs in the ValidationBehavior before the
/// handler. Only checks that the ids are non-empty — whether the assignee is actually a project member
/// is a business rule verified against the database inside the handler, not a shape concern.
/// </summary>
public sealed class AssignTaskValidator : AbstractValidator<AssignTaskCommand>
{
    public AssignTaskValidator()
    {
        RuleFor(command => command.TaskId)
            .NotEmpty();

        RuleFor(command => command.AssigneeId)
            .NotEmpty();
    }
}
