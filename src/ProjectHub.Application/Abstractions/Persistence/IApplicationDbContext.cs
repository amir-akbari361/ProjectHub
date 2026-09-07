using Microsoft.EntityFrameworkCore;
using ProjectHub.Domain.Entities;

namespace ProjectHub.Application.Abstractions.Persistence;

/// <summary>
/// Read-only projection of the write DbContext exposed to the Application layer.
/// Only aggregate roots are exposed — never join tables (they must be reached through
/// the owning aggregate root). Keeps Application ignorant of EF-specific APIs beyond
/// <see cref="DbSet{TEntity}"/> for LINQ composition inside query handlers.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<Project> Projects { get; }
    DbSet<ProjectTask> ProjectTasks { get; }
    DbSet<Sprint> Sprints { get; }
    DbSet<Comment> Comments { get; }
    DbSet<Attachment> Attachments { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<AuditLog> AuditLogs { get; }

    /// <summary>
    /// Users whose email address, first name or last name contains <paramref name="term"/> — returned as a
    /// composable query the caller keeps filtering, ordering, paging and projecting.
    /// </summary>
    /// <remarks>
    /// WHY THIS IS A METHOD ON THE SEAM RATHER THAN A PREDICATE AT THE CALL SITE
    /// <see cref="User.Email"/> is persisted through a value converter (the Email value object &lt;-&gt; nvarchar).
    /// EF can compare the whole converted value and can materialise it in a projection, but it cannot express a
    /// PARTIAL match on it in a <c>WHERE</c> clause: reaching inside the converted type does not translate, and
    /// naming the column with <c>EF.Property&lt;string&gt;</c> still routes the LIKE pattern through the converter,
    /// which throws while binding the parameter. Every way out needs the physical column name — which is
    /// Persistence's business, not the Application layer's. So the layer states the search it wants and stays
    /// ignorant of how the provider delivers it, exactly as it does for the DbSets above.
    /// </remarks>
    IQueryable<User> SearchUsers(string term);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
