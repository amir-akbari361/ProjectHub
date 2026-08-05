using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Application.Common;

namespace ProjectHub.Application.Features.ProjectMembers.ListMembers;

/// <summary>
/// Handles <see cref="ListMembersQuery"/>. A READ-side handler: it confirms the caller is a member of the
/// project, then composes a single SQL statement that joins each <c>ProjectMember</c> to its <c>User</c>
/// and projects straight into <see cref="MemberResponse"/>. It NEVER materializes the <c>Project</c>
/// aggregate — the read path stays free of change tracking and domain behavior.
/// </summary>
public sealed class ListMembersQueryHandler
    : IQueryHandler<ListMembersQuery, IReadOnlyList<MemberResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<ListMembersQueryHandler> _logger;

    public ListMembersQueryHandler(
        IApplicationDbContext context,
        ICurrentUser currentUser,
        ILogger<ListMembersQueryHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<MemberResponse>>> Handle(
        ListMembersQuery request, CancellationToken cancellationToken)
    {
        // 1. Resolve the caller. The roster is scoped to membership => 401 before any DB access.
        if (_currentUser.UserId is not { } userId)
        {
            _logger.LogWarning("ListMembers reached the handler without an authenticated user.");
            return Result.Failure<IReadOnlyList<MemberResponse>>(Error.Unauthorized(
                "Members.Unauthenticated",
                "You must be signed in to view project members."));
        }

        // 2. Visibility: the caller must themselves be a member. A cheap EXISTS over project -> members.
        //    Unknown project and non-member both collapse into the SAME NotFound (no disclosure).
        var projectVisible = await _context.Projects
            .AsNoTracking()
            .AnyAsync(
                p => p.Id == request.ProjectId && p.Members.Any(m => m.UserId == userId),
                cancellationToken);

        if (!projectVisible)
        {
            _logger.LogInformation(
                "ListMembers: project {ProjectId} not found or not visible to user {UserId}.",
                request.ProjectId, userId);
            return Result.Failure<IReadOnlyList<MemberResponse>>(
                MemberErrors.ProjectNotFound(request.ProjectId));
        }

        // 3. Project the roster in ONE query. IApplicationDbContext exposes only aggregate ROOTS, so we
        //    reach the members through their owning Project aggregate with SelectMany, then join to Users
        //    for the display fields. AsNoTracking() — pure read; the global soft-delete filter already
        //    excludes deleted members and users. Ordered Owner→Maintainer→… then by join time so the
        //    roster renders in a stable, meaningful order.
        var members = await _context.Projects
            .AsNoTracking()
            .Where(p => p.Id == request.ProjectId)
            .SelectMany(p => p.Members)
            .OrderBy(m => m.Role)
            .ThenBy(m => m.CreatedAtUtc)
            .Join(
                _context.Users,
                member => member.UserId,
                user => user.Id,
                (member, user) => new MemberResponse(
                    member.UserId,
                    user.Email.Value,
                    user.FirstName + " " + user.LastName,
                    member.Role,
                    member.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Listed {Count} members for project {ProjectId}.", members.Count, request.ProjectId);

        return members;
    }
}
