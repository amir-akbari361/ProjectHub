using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Domain.Entities;
using ProjectHub.Domain.Primitives;

namespace ProjectHub.Persistence.Interceptors;

/// <summary>
/// Writes an append-only audit trail. On every save it scans the change tracker for the audited
/// aggregate types and materialises one <see cref="AuditLog"/> row per changed entity — capturing the
/// action (Created/Updated/Deleted), a field-level JSON diff of what changed, who did it, and the
/// owning project — then enlists those rows in the SAME unit of work so the trail is atomic with the
/// change that produced it.
/// </summary>
/// <remarks>
/// ORDERING: this interceptor must be registered AFTER <see cref="SoftDeleteInterceptor"/> so that, by
/// the time it runs, a logical delete already presents as <see cref="EntityState.Modified"/> with
/// <c>IsDeleted</c> flipped to <c>true</c> — which we translate back into a "Deleted" action. A raw
/// <see cref="EntityState.Deleted"/> only occurs for the (currently unused) hard-delete path.
///
/// CHANGE DETECTION: reading <c>ChangeTracker.Entries&lt;Entity&gt;()</c> triggers EF's automatic
/// <c>DetectChanges()</c>, so mutations made through domain methods (which assign private setters /
/// backing fields) are already reflected in each entry's state and per-property <c>IsModified</c>
/// flags. This is the same mechanism the soft-delete interceptor relies on.
///
/// VALUE-OBJECT MAPPINGS the diff walker accounts for:
///  • <c>Project.Name</c> / <c>ProjectTask.Title</c> are ComplexProperty mappings — their leaf
///    (<c>Name.Value</c> / <c>Title.Value</c>) lives under <see cref="EntityEntry.ComplexProperties"/>,
///    NOT <see cref="EntityEntry.Properties"/>, so both collections are walked.
///  • <c>Comment.Body</c> and the enums are scalar value-converter mappings — they appear in
///    <c>Properties</c>, but a converted property's <c>CurrentValue</c> is the MODEL instance
///    (e.g. a <see cref="ProjectHub.Domain.ValueObjects.CommentBody"/>), so <see cref="FormatValue"/>
///    unwraps a public string <c>Value</c> rather than calling <c>ToString()</c>.
///  • <c>Sprint.Schedule</c> and <c>Attachment.File</c> are OwnsOne owned entities — separate tracked
///    entries, so their sub-field changes are NOT captured in the parent diff (a documented gap). The
///    parent's own <c>MarkUpdated</c> still marks it Modified, so the ACTION is recorded even when the
///    diff is empty.
/// </remarks>
public sealed class AuditLogInterceptor : SaveChangesInterceptor
{
    // The aggregate types whose changes are recorded. The set mirrors the read-side allow-list in
    // ListAuditLogsValidator; the entity NAME persisted on each row is the CLR type name (e.g. "Project"),
    // which matches that list exactly.
    private static readonly HashSet<Type> AuditedTypes =
    [
        typeof(Project),
        typeof(ProjectTask),
        typeof(Sprint),
        typeof(ProjectMember),
        typeof(Comment),
        typeof(Attachment),
    ];

    // Infrastructure/bookkeeping columns that carry no business meaning in a diff. IsDeleted is excluded
    // too — a soft delete is conveyed by the "Deleted" action, so surfacing "IsDeleted: false → true"
    // would be redundant noise.
    private static readonly HashSet<string> IgnoredProperties = new(StringComparer.Ordinal)
    {
        nameof(Entity.Id),
        nameof(Entity.CreatedAtUtc),
        nameof(Entity.UpdatedAtUtc),
        nameof(Entity.DeletedAtUtc),
        nameof(Entity.CreatedBy),
        nameof(Entity.UpdatedBy),
        nameof(Entity.IsDeleted),
        nameof(Entity.RowVersion),
    };

    // CamelCase turns the ChangePair members From/To into "from"/"to". Dictionary keys (the property
    // names) are left untouched — DictionaryKeyPolicy is deliberately not set.
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;

    public AuditLogInterceptor(ICurrentUser currentUser, IDateTimeProvider dateTimeProvider)
    {
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            await WriteAuditLogsAsync(eventData.Context, cancellationToken);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private async Task WriteAuditLogsAsync(DbContext context, CancellationToken cancellationToken)
    {
        // Snapshot the audited entries BEFORE adding any audit rows. We never re-scan, and AuditLog is
        // not in the allow-list, so the rows we add below can never audit themselves.
        var auditedEntries = context.ChangeTracker
            .Entries<Entity>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(entry => AuditedTypes.Contains(entry.Entity.GetType()))
            .ToList();

        if (auditedEntries.Count == 0)
        {
            return;
        }

        var utcNow = _dateTimeProvider.UtcNow;

        // Null for the seeder / any system-initiated save outside a request — recorded as an
        // unattributed change rather than being dropped.
        var performedBy = _currentUser.UserId;

        var logs = new List<AuditLog>(auditedEntries.Count);

        // Locate each row's owning project from tracked state alone, then settle the leftovers (comments
        // and attachments whose parent task was not part of this unit of work) in ONE query.
        var located = auditedEntries
            .Select(entry => (Entry: entry, Location: LocateProject(entry, context)))
            .ToList();

        var parentProjects = await ResolveParentProjectsAsync(
            context, located.Select(item => item.Location), cancellationToken);

        foreach (var (entry, location) in located)
        {
            var (action, changes) = Describe(entry);

            var projectId = location.ProjectId
                ?? (location.TaskId is { } taskId && parentProjects.TryGetValue(taskId, out var owner)
                    ? owner
                    : null);

            logs.Add(AuditLog.Record(
                entry.Entity.GetType().Name,
                entry.Entity.Id,
                action,
                utcNow,
                performedBy,
                changes,
                projectId));
        }

        // Added via the change-tracker API (eager state), so these rows are included in this very
        // SaveChanges — the audit trail commits in the same transaction as the audited change.
        context.Set<AuditLog>().AddRange(logs);
    }

    private static (string Action, string? Changes) Describe(EntityEntry entry)
    {
        switch (entry.State)
        {
            case EntityState.Added:
                return ("Created", BuildChanges(entry, includeUnmodified: true));

            case EntityState.Deleted:
                // Hard delete — no aggregate takes this path today (Remove is intercepted into a soft
                // delete), but handle it so the action is never mislabelled if one ever does.
                return ("Deleted", null);

            default:
                return IsSoftDelete(entry)
                    ? ("Deleted", null)
                    : ("Updated", BuildChanges(entry, includeUnmodified: false));
        }
    }

    // A soft delete arrives here (after SoftDeleteInterceptor) as a Modified entry whose IsDeleted
    // column was just flipped to true.
    private static bool IsSoftDelete(EntityEntry entry)
    {
        var isDeleted = entry.Properties
            .FirstOrDefault(property => property.Metadata.Name == nameof(Entity.IsDeleted));

        return isDeleted is { IsModified: true } && isDeleted.CurrentValue is true;
    }

    // Serialises a {"prop": {"from": ..., "to": ...}} diff. For a Created row every populated field is
    // recorded as from=null→to=value; for an Updated row only the modified fields are recorded. Returns
    // null when there is nothing to show (an empty object would be noise), which still leaves the row's
    // action to carry the meaning.
    private static string? BuildChanges(EntityEntry entry, bool includeUnmodified)
    {
        var changes = new Dictionary<string, ChangePair>(StringComparer.Ordinal);

        foreach (var (name, property) in EnumerateLeafProperties(entry))
        {
            if (includeUnmodified)
            {
                var current = FormatValue(property.CurrentValue);
                if (current is not null)
                {
                    changes[name] = new ChangePair(null, current);
                }
            }
            else if (property.IsModified)
            {
                changes[name] = new ChangePair(
                    FormatValue(property.OriginalValue),
                    FormatValue(property.CurrentValue));
            }
        }

        return changes.Count == 0 ? null : JsonSerializer.Serialize(changes, SerializerOptions);
    }

    // Yields the scalar leaves worth diffing: direct scalar properties (including value-converter
    // mappings and enums) and the leaves of ComplexProperty value objects (dotted, e.g. "Name.Value").
    // Owned entities (OwnsOne) are separate entries and are intentionally not reached from here.
    private static IEnumerable<(string Name, PropertyEntry Property)> EnumerateLeafProperties(EntityEntry entry)
    {
        foreach (var property in entry.Properties)
        {
            if (!IgnoredProperties.Contains(property.Metadata.Name))
            {
                yield return (property.Metadata.Name, property);
            }
        }

        foreach (var complex in entry.ComplexProperties)
        {
            foreach (var leaf in complex.Properties)
            {
                if (!IgnoredProperties.Contains(leaf.Metadata.Name))
                {
                    yield return ($"{complex.Metadata.Name}.{leaf.Metadata.Name}", leaf);
                }
            }
        }
    }

    // Where each audited row belongs. Project/Task/Sprint/Member carry the id on the entity itself.
    // Comment/Attachment only know their parent TASK, so both halves are returned: the project if the
    // parent happens to be tracked in this unit of work (free), and the TaskId for the batched lookup
    // below to settle otherwise.
    private static (Guid? ProjectId, Guid? TaskId) LocateProject(EntityEntry entry, DbContext context) =>
        entry.Entity switch
        {
            Project project => (project.Id, null),
            ProjectTask task => (task.ProjectId, null),
            Sprint sprint => (sprint.ProjectId, null),
            ProjectMember member => (member.ProjectId, null),
            Comment comment => (TrackedTaskProject(context, comment.TaskId), comment.TaskId),
            Attachment attachment => (TrackedTaskProject(context, attachment.TaskId), attachment.TaskId),
            _ => (null, null),
        };

    private static Guid? TrackedTaskProject(DbContext context, Guid taskId) => context.ChangeTracker
        .Entries<ProjectTask>()
        .FirstOrDefault(entry => entry.Entity.Id == taskId)
        ?.Entity.ProjectId;

    /// <summary>
    /// Maps parent-task id to owning project id for the comment/attachment rows whose task is not tracked
    /// in this unit of work — which is the normal case: the command handlers authorize against the project
    /// with a no-tracking scalar projection, so nothing of the task ever enters the change tracker.
    /// </summary>
    /// <remarks>
    /// WHY A QUERY MID-SAVE IS ACCEPTABLE HERE
    /// This runs inside <c>SavingChangesAsync</c>, before EF has sent any command, and it is awaited — so it
    /// is sequential with the save rather than concurrent with it. Two deliberate choices keep it inert:
    /// <c>AsNoTracking</c> with a projection to two scalars means nothing is materialised into the change
    /// tracker we are in the middle of scanning, and one batched <c>IN</c> query means the cost is a single
    /// round-trip regardless of how many comments a save touches (in practice: one).
    ///
    /// WHY QUERY FILTERS ARE IGNORED
    /// A comment on a task that was soft-deleted earlier still belongs to that task's project, and its audit
    /// row should say so. Ownership is a historical fact; it must not evaporate because the parent is gone.
    ///
    /// A row left unresolved is written with a null ProjectId rather than being dropped — the trail is
    /// append-only and complete, and the read-side authorization resolves ownership from the live entity, so
    /// a null here only makes the row invisible to the project FILTER, never wrongly visible.
    /// </remarks>
    private static async Task<Dictionary<Guid, Guid>> ResolveParentProjectsAsync(
        DbContext context,
        IEnumerable<(Guid? ProjectId, Guid? TaskId)> locations,
        CancellationToken cancellationToken)
    {
        var taskIds = locations
            .Where(location => location.ProjectId is null && location.TaskId is not null)
            .Select(location => location.TaskId!.Value)
            .Distinct()
            .ToList();

        if (taskIds.Count == 0)
        {
            return [];
        }

        var rows = await context.Set<ProjectTask>()
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(task => taskIds.Contains(task.Id))
            .Select(task => new { task.Id, task.ProjectId })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.Id, row => row.ProjectId);
    }

    // Renders a tracked value as a stable, human-readable string for the diff. Enums use their name,
    // dates use round-trip ISO 8601, numbers use the invariant culture, and a converted value object
    // (whose CurrentValue is the model instance) is unwrapped via its public string Value.
    private static string? FormatValue(object? value) => value switch
    {
        null => null,
        string text => text,
        bool flag => flag ? "true" : "false",
        Enum enumeration => enumeration.ToString(),
        DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
        DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => UnwrapValueObject(value),
    };

    private static string? UnwrapValueObject(object value)
    {
        var valueProperty = value.GetType().GetProperty("Value");

        return valueProperty is not null && valueProperty.PropertyType == typeof(string)
            ? valueProperty.GetValue(value) as string
            : value.ToString();
    }

    // Concrete (non-object) value type, so System.Text.Json serialises it directly with no polymorphism
    // surprises. CamelCase in SerializerOptions renders the members as "from"/"to".
    private sealed record ChangePair(string? From, string? To);
}
