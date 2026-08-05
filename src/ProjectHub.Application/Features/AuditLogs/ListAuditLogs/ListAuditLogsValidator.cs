using FluentValidation;

namespace ProjectHub.Application.Features.AuditLogs.ListAuditLogs;

/// <summary>
/// Validates <see cref="ListAuditLogsQuery"/> BEFORE it reaches the handler (via the validation pipeline
/// behavior). Guards the two things a client controls: the entity name (must be a known, audited type)
/// and the paging window (must be sane). Invalid input fails fast with 400 — the handler only ever sees
/// well-formed queries.
/// </summary>
/// <remarks>
/// WHY AN ALLOW-LIST FOR <c>EntityName</c>?
/// The audit store is keyed by a free-form name, so an unconstrained string would let a caller probe for
/// arbitrary keys or inject unexpected values into the WHERE clause parameter. Constraining it to the
/// entity types we actually audit turns a polymorphic-but-open surface into a closed one — defense in
/// depth even though EF parameterizes the query.
/// </remarks>
public sealed class ListAuditLogsValidator : AbstractValidator<ListAuditLogsQuery>
{
    /// <summary>
    /// The entity types that ProjectHub records an audit trail for. Kept in sync with the writers that
    /// emit audit rows. Case-insensitive comparison is applied in the rule so callers needn't match casing.
    /// </summary>
    private static readonly string[] AuditableEntities =
    {
        "Project",
        "ProjectTask",
        "Sprint",
        "ProjectMember",
        "Comment",
        "Attachment",
    };

    public ListAuditLogsValidator()
    {
        RuleFor(q => q.EntityName)
            .NotEmpty().WithMessage("Entity name is required.")
            .Must(name => AuditableEntities.Contains(name, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Entity name must be one of: {string.Join(", ", AuditableEntities)}.");

        RuleFor(q => q.EntityId)
            .NotEmpty().WithMessage("Entity id is required.");

        RuleFor(q => q.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("Page number must be at least 1.");

        RuleFor(q => q.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Page size must be between 1 and 100.");
    }
}
