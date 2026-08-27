using FluentValidation;

namespace ProjectHub.Application.Features.Sprints.CreateSprint;

/// <summary>
/// Validates the SHAPE of a <see cref="CreateSprintCommand"/> before it reaches the handler. Invoked by
/// the MediatR <c>ValidationBehavior</c> pipeline, so a malformed request is rejected with a 400 before
/// any domain code or database round-trip runs. Structural rules only (presence, length, ordering of the
/// two dates) — business rules (membership, archived project) stay in the handler/domain.
/// </summary>
public sealed class CreateSprintValidator : AbstractValidator<CreateSprintCommand>
{
    // Matches the max length enforced by the EF mapping (SprintConfiguration: Name HasMaxLength(200)).
    private const int NameMaxLength = 200;

    public CreateSprintValidator()
    {
        RuleFor(command => command.ProjectId)
            .NotEmpty();

        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(NameMaxLength);

        // The end must be strictly after the start — the same invariant DateRange.Create enforces, checked
        // here so a bad range is a clean 400 rather than a domain throw the handler has to translate.
        RuleFor(command => command.EndUtc)
            .GreaterThan(command => command.StartUtc)
            .WithMessage("The sprint's end date must be after its start date.");
    }
}
