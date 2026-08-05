using FluentValidation;

namespace ProjectHub.Application.Features.Tasks.CreateTask;

/// <summary>
/// Validates the SHAPE of a <see cref="CreateTaskCommand"/> before it reaches the handler. Invoked by
/// the MediatR <c>ValidationBehavior</c> pipeline, so a malformed request is rejected with a 400 before
/// any domain code or database round-trip runs. Structural rules only (presence, length, enum range) —
/// business rules (membership, archived project) stay in the handler/domain.
/// </summary>
public sealed class CreateTaskValidator : AbstractValidator<CreateTaskCommand>
{
    private const int TitleMaxLength = 500;
    private const int DescriptionMaxLength = 5000;

    public CreateTaskValidator()
    {
        RuleFor(command => command.ProjectId)
            .NotEmpty();

        RuleFor(command => command.Title)
            .NotEmpty()
            .MaximumLength(TitleMaxLength);

        RuleFor(command => command.Description)
            .MaximumLength(DescriptionMaxLength);

        // IsInEnum rejects out-of-range integers a client could POST directly (e.g., Priority = 99),
        // which model binding would otherwise accept as a raw enum value.
        RuleFor(command => command.Priority)
            .IsInEnum();
    }
}
