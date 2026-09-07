using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Common;

namespace ProjectHub.Application.Features.Admin.Users.ListUsers;

/// <summary>
/// Admin-only query listing every account in the workspace, paged, with an optional text search and an
/// optional active/inactive filter. This is the roster the user-administration console renders.
/// </summary>
/// <remarks>
/// WHY IS THIS NOT A VARIANT OF THE MEMBER LOOKUP?
/// <c>ListMembers</c> answers "who is on this project" and is scoped by membership. This one crosses every
/// boundary — it includes accounts the caller shares no project with, plus deactivated ones — which is
/// exactly why it is Admin-only. Keeping them apart means the membership-scoped list can never leak the
/// full directory because a parameter defaulted to "all".
///
/// WHY DOES <c>IsActive</c> DEFAULT TO NULL RATHER THAN TRUE?
/// An administration console's job includes finding the accounts that are switched OFF — that is where
/// reactivation starts. Defaulting to active-only would hide the very rows the page exists to manage, so
/// null means "both" and the UI offers an explicit filter.
/// </remarks>
public sealed record ListUsersQuery(
    string? SearchTerm = null,
    bool? IsActive = null,
    int PageNumber = 1,
    int PageSize = 20)
    : IQuery<PagedList<AdminUserResponse>>;
