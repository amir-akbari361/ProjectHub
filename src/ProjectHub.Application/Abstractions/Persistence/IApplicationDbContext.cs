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

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
