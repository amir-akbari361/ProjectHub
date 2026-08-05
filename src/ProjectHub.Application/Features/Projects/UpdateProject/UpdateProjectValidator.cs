using FluentValidation;

namespace ProjectHub.Application.Features.Projects.UpdateProject;

/// <summary>
/// Validates the SHAPE of an <see cref="UpdateProjectCommand"/> before the handler runs. Mirrors
/// <c>CreateProjectValidator</c> because the fields are the same — presence and length rules are a
/// structural concern, checked once in the pipeline so a malformed edit is a clean 400, not a 500 from
/// deep inside the value object.
/// </summary>
/// <remarks>
/// WHY VALIDATE <see cref="UpdateProjectCommand.ProjectId"/> IS NOT EMPTY?
/// The id comes from the route, and the <c>{id:guid}</c> constraint already guarantees it parses. The
/// NotEmpty rule guards the OTHER entry points (a future internal caller, a test) from dispatching a
/// command with <c>Guid.Empty</c>, which would never match a row and waste a round-trip. Defense in
/// depth: the validator does not assume the only caller is the HTTP route.
/// </remarks>
public sealed class UpdateProjectValidator : AbstractValidator<UpdateProjectCommand>
{
    private const int NameMaxLength = 200;
    private const int DescriptionMaxLength = 2000;

    public UpdateProjectValidator()
    {
        RuleFor(command => command.ProjectId)
            .NotEmpty();

        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(NameMaxLength);

        RuleFor(command => command.Description)
            .MaximumLength(DescriptionMaxLength);
    }
}
