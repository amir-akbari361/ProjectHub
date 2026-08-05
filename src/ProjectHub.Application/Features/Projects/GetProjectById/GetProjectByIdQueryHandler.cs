using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Application.Common;

namespace ProjectHub.Application.Features.Projects.GetProjectById;

/// <summary>
/// Handles <see cref="GetProjectByIdQuery"/>. This is a READ-side handler and therefore looks very
/// different from a command handler: it depends on <see cref="IApplicationDbContext"/> (for LINQ
/// composition) rather than <c>IRepository</c>/<c>IUnitOfWork</c>, it NEVER calls SaveChanges, and it
/// projects straight into a <see cref="ProjectResponse"/> DTO. There is no <c>Project</c> aggregate
/// materialized at all — the query shapes only the columns the client needs.
/// </summary>
/// <remarks>
/// WHY <c>IApplicationDbContext</c> AND NOT THE REPOSITORY?
/// The repository is the write-side port: it loads whole aggregates so we can invoke behavior and
/// enforce invariants. On the read side we want the opposite — a thin, tailored projection with no
/// tracking and no aggregate hydration. Depending on the context's <c>DbSet</c>s lets us write a
/// single efficient SQL statement (SELECT + JOIN) via <c>Select</c>, which is the whole point of
/// separating queries from commands in CQRS.
///
/// WHY THE MEMBERSHIP CHECK?
/// A project is private to its members. "Not found" and "you're not a member" are deliberately
/// COLLAPSED into a single 404: revealing a 403 would leak the existence of a project id to outsiders
/// (an enumeration/`information-disclosure` vector). So a non-member gets the exact same response as
/// if the id never existed.
/// </remarks>
public sealed class GetProjectByIdQueryHandler
    : IQueryHandler<GetProjectByIdQuery, ProjectResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<GetProjectByIdQueryHandler> _logger;

    public GetProjectByIdQueryHandler(
        IApplicationDbContext context,
        ICurrentUser currentUser,
        ILogger<GetProjectByIdQueryHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<ProjectResponse>> Handle(
        GetProjectByIdQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Resolve the caller. Reads are also attributed to a known user because visibility is scoped
        //    to membership. No principal means the endpoint was reached without auth — fail fast with a
        //    401 rather than run a query that could never legitimately return data.
        if (_currentUser.UserId is not { } userId)
        {
            _logger.LogWarning("GetProjectById reached the handler without an authenticated user.");
            return Result.Failure<ProjectResponse>(Error.Unauthorized(
                "Projects.Unauthenticated",
                "You must be signed in to view a project."));
        }

        // 2. Compose the read query. AsNoTracking() because we will never mutate what we load here —
        //    it skips EF's change-tracker snapshotting, the single biggest perf win for pure reads.
        //    The global soft-delete filter already hides deleted projects, so we don't repeat it.
        //    We project DIRECTLY into the DTO inside the Select so EF translates the whole thing to
        //    ONE SQL round-trip and materializes only the columns we asked for (no over-fetching).
        var project = await _context.Projects
            .AsNoTracking()
            .Where(p => p.Id == request.ProjectId && p.Members.Any(m => m.UserId == userId))
            .Select(p => new ProjectResponse(
                p.Id,
                p.Name.Value,
                p.Description,
                p.Status,
                p.CreatedAtUtc,
                p.Members
                    .Select(m => new ProjectMemberResponse(m.UserId, m.Role))
                    .ToList()))
            .SingleOrDefaultAsync(cancellationToken);

        // 3. Collapse "doesn't exist" and "not a member" into one NotFound. Because the WHERE clause
        //    requires the caller to be a member, a null here means EITHER the id is unknown OR the
        //    caller has no access — and we intentionally cannot tell the client which, to avoid leaking
        //    the existence of projects the caller may not see.
        if (project is null)
        {
            _logger.LogInformation(
                "Project {ProjectId} not found or not visible to user {UserId}.",
                request.ProjectId, userId);
            return Result.Failure<ProjectResponse>(ProjectErrors.NotFound(request.ProjectId));
        }

        return project;
    }
}
