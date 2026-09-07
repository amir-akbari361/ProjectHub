using FluentValidation;

namespace ProjectHub.Application.Features.AuditLogs.ListAllAuditLogs;

/// <summary>
/// Validates <see cref="ListAllAuditLogsQuery"/> before it reaches the handler. Every filter is optional, so
/// each rule is conditional: a null filter is simply "don't narrow by this", not an error. What IS checked is
/// that a SUPPLIED filter is well-formed — an allow-listed entity name, a non-empty id, a coherent date
/// range, and a sane paging window.
/// </summary>
/// <remarks>
/// The entity-name allow-list is duplicated from <c>ListAuditLogsValidator</c> rather than shared. That is a
/// deliberate call: these two lists answer different questions ("which trails may be READ" vs. "which names
/// may be FILTERED on") and will drift apart the moment an entity becomes filterable before it is auditable.
/// Both are short, self-documenting, and fail loudly in tests if they disagree with the writer's allow-list.
/// </remarks>
public sealed class ListAllAuditLogsValidator : AbstractValidator<ListAllAuditLogsQuery>
{
    private static readonly string[] AuditableEntities =
    {
        "Project",
        "ProjectTask",
        "Sprint",
        "ProjectMember",
        "Comment",
        "Attachment",
    };

    public ListAllAuditLogsValidator()
    {
        // Only constrain the name when one was given — omitting it means "all entity types".
        When(q => !string.IsNullOrWhiteSpace(q.EntityName), () =>
        {
            RuleFor(q => q.EntityName)
                .Must(name => AuditableEntities.Contains(name, StringComparer.OrdinalIgnoreCase))
                .WithMessage($"Entity name must be one of: {string.Join(", ", AuditableEntities)}.");
        });

        // An explicitly-supplied but empty GUID is a caller bug (usually an uninitialised field), not a
        // request to match rows with an empty id — reject it rather than silently returning nothing.
        RuleFor(q => q.ProjectId)
            .NotEqual(Guid.Empty).WithMessage("Project id must not be empty when supplied.")
            .When(q => q.ProjectId.HasValue);

        RuleFor(q => q.PerformedBy)
            .NotEqual(Guid.Empty).WithMessage("Performed-by must not be empty when supplied.")
            .When(q => q.PerformedBy.HasValue);

        // An inverted range would return zero rows and look like "no activity" rather than a mistake.
        RuleFor(q => q.FromUtc)
            .LessThanOrEqualTo(q => q.ToUtc)
            .WithMessage("The start of the date range must not be later than its end.")
            .When(q => q.FromUtc.HasValue && q.ToUtc.HasValue);

        RuleFor(q => q.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("Page number must be at least 1.");

        RuleFor(q => q.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Page size must be between 1 and 100.");
    }
}
