using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Application.Common;

namespace ProjectHub.Application.Features.Admin.Users.ListUsers;

/// <summary>
/// Handles <see cref="ListUsersQuery"/> — the Admin user directory. Applies the optional search and status
/// filters, pages alphabetically by email, and projects into <see cref="AdminUserResponse"/> with each
/// account's global role names.
/// </summary>
/// <remarks>
/// WHY ORDER BY EMAIL RATHER THAN NEWEST-FIRST?
/// The other list views in this app are feeds, read newest-first. A directory is not a feed: it is something
/// you scan for a particular person, so a stable alphabetical order is what makes it usable — and it means a
/// given account stays on the same page between visits.
///
/// WHY IS THE SEARCH A CONTAINS OVER THREE COLUMNS?
/// An admin looking someone up has whatever fragment they were given — half an address, a first name, a
/// surname. Matching any of the three is what makes one box sufficient. It translates to SQL LIKE
/// '%term%', which cannot use an index; acceptable because this page is used occasionally by a handful of
/// people, and the result is paged. If the directory ever grows to the point where this matters, the fix is
/// full-text search, not a narrower query. The match is issued through
/// <see cref="IApplicationDbContext.SearchUsers"/> because the email column is value-converted — see there.
///
/// WHY PROJECT THE ROLES INSTEAD OF INCLUDING THEM?
/// <c>Include</c> would materialise <c>User</c> aggregates with their <c>UserRole</c> children just to read
/// two strings. Projecting the names keeps this a read-only, change-tracker-free query and emits one
/// statement, consistent with every other list handler here.
/// </remarks>
public sealed class ListUsersQueryHandler
    : IQueryHandler<ListUsersQuery, PagedList<AdminUserResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<ListUsersQueryHandler> _logger;

    public ListUsersQueryHandler(
        IApplicationDbContext context,
        ICurrentUser currentUser,
        ILogger<ListUsersQueryHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<PagedList<AdminUserResponse>>> Handle(
        ListUsersQuery request,
        CancellationToken cancellationToken)
    {
        var access = AdminAccess.Resolve(_currentUser);

        if (access.IsFailure)
        {
            _logger.LogWarning("ListUsers was refused: {ErrorCode}.", access.Error.Code);
            return Result.Failure<PagedList<AdminUserResponse>>(access.Error);
        }

        var query = _context.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            // Trimmed so a stray space from the search box does not stop every row from matching.
            //
            // The match itself is delegated to the persistence seam rather than written as a predicate here:
            // `Email` is value-converted, which makes a partial match on it inexpressible in LINQ (it either fails
            // to translate or throws while binding the LIKE parameter), and the way around it needs the physical
            // column name. SearchUsers returns a normal composable IQueryable, so the status filter, ordering,
            // paging and projection below are unaffected and still execute as one statement.
            //
            // `!IsDeleted` is restated even though the soft-delete query filter also applies: a deleted account
            // must never surface in an admin directory, and that guarantee should not rest on a convention this
            // method cannot see. A duplicated predicate costs nothing.
            query = _context.SearchUsers(request.SearchTerm.Trim())
                .AsNoTracking()
                .Where(user => !user.IsDeleted);
        }

        if (request.IsActive is { } isActive)
        {
            query = query.Where(user => user.IsActive == isActive);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(user => user.Email)
            .ThenBy(user => user.Id)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(user => new AdminUserResponse(
                user.Id,
                user.Email.Value,
                user.FirstName,
                user.LastName,
                user.IsActive,
                user.IsEmailConfirmed,
                user.Roles.Select(assignment => assignment.Role.Name).ToList(),
                user.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Admin {AdminId} listed {Count} of {Total} users (page {Page}).",
            access.Value, items.Count, totalCount, request.PageNumber);

        return new PagedList<AdminUserResponse>(
            items, totalCount, request.PageNumber, request.PageSize);
    }
}
