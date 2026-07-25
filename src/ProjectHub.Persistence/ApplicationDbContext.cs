using Microsoft.EntityFrameworkCore;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Domain.Entities;

namespace ProjectHub.Persistence;

/// <summary>
/// The single EF Core write model for ProjectHub. It lives in Persistence (the only project that
/// may reference EF Core's provider) and implements the Application-owned <see cref="IApplicationDbContext"/>
/// so query handlers depend on the abstraction, never on this concrete type. Aggregate mapping is
/// discovered by assembly scan; the audit/soft-delete/domain-event cross-cutting concerns are added
/// by interceptors registered in <see cref="DependencyInjection"/>, keeping this class free of them.
/// </summary>
public sealed class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<ProjectTask> ProjectTasks => Set<ProjectTask>();

    public DbSet<Sprint> Sprints => Set<Sprint>();

    public DbSet<Comment> Comments => Set<Comment>();

    public DbSet<Attachment> Attachments => Set<Attachment>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Discover every IEntityTypeConfiguration in this assembly (User/Project/Task/... plus the
        // internal join-table configs) instead of registering each by hand. One line, zero drift:
        // adding a new configuration class wires itself up automatically.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
