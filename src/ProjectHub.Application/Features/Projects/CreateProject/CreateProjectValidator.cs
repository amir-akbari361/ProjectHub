using FluentValidation;

namespace ProjectHub.Application.Features.Projects.CreateProject;

/// <summary>
/// Validates the SHAPE of a <see cref="CreateProjectCommand"/> before it reaches the handler. This
/// is invoked by the MediatR <c>ValidationBehavior</c> pipeline, so a malformed request is rejected
/// with a 400 before any domain code or database round-trip runs. We validate only structural rules
/// here (presence, length) — business rules (e.g., archived-project checks) stay in the domain.
/// </summary>
/// <remarks>
/// WHY DUPLICATE THE LENGTH RULE THAT <c>ProjectName</c> ALREADY ENFORCES?
/// The value object is the last line of defense (an invariant that can never be violated), but by the
/// time it throws we're already inside the handler and the failure surfaces as a 500. Validating up
/// front turns the same rule into a clean 400 with a field-level message the client can display. The
/// two are complementary: FluentValidation for a good UX, the value object for correctness guarantees.
/// </remarks>
public sealed class CreateProjectValidator : AbstractValidator<CreateProjectCommand>
{
    private const int NameMaxLength = 200;
    private const int DescriptionMaxLength = 2000;

    public CreateProjectValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(NameMaxLength);

        // Description is optional, so we only constrain its length. FluentValidation's MaximumLength
        // treats null as valid, so no explicit null guard is needed here.
        RuleFor(command => command.Description)
            .MaximumLength(DescriptionMaxLength);
    }
}
