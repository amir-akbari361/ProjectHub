using Microsoft.EntityFrameworkCore;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Domain.Entities;
using ProjectHub.Persistence.Constants;

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

    /// <inheritdoc />
    /// <remarks>
    /// The one query in this class expressed as SQL, and only because LINQ cannot express it: a partial match on
    /// the value-converted <c>Email</c> column either fails to translate or throws while binding the LIKE
    /// parameter (see the interface for the full account). Writing it here rather than in the query handler is the
    /// point — the physical column names belong to this project, and the caller receives a normal composable
    /// <see cref="IQueryable{T}"/>.
    ///
    /// <c>FromSqlRaw</c> is safe here despite the name: the only interpolated text is a compile-time schema
    /// constant, and the search term is passed as a positional parameter, so a term containing a quote or a
    /// bracket is data and never syntax. The pattern is repeated as three parameters rather than reusing one
    /// placeholder, which keeps the substitution unambiguous.
    ///
    /// <c>SELECT *</c> is deliberate: EF needs every mapped column of the entity, and listing them by hand would
    /// silently break the moment a property is added. EF wraps this statement as a subquery, so the soft-delete
    /// query filter and all of the caller's composition still run server-side in one round trip.
    /// </remarks>
    public IQueryable<User> SearchUsers(string term)
    {
        // Wildcards are added around the term instead of inside the SQL so the whole thing stays one bound
        // parameter. A % or _ typed by the user is left as a wildcard rather than escaped — for a search box that
        // is the more useful reading, and it cannot escape the parameter.
        var pattern = $"%{term}%";

        return Set<User>().FromSqlRaw(
            $"SELECT * FROM [{Schemas.Identity}].[users] "
            + "WHERE [email] LIKE {0} OR [FirstName] LIKE {1} OR [LastName] LIKE {2}",
            pattern, pattern, pattern);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Discover every IEntityTypeConfiguration in this assembly (User/Project/Task/... plus the
        // internal join-table configs) instead of registering each by hand. One line, zero drift:
        // adding a new configuration class wires itself up automatically.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
