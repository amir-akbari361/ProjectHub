using System.Collections.Generic;
using ProjectHub.Application.Abstractions.Messaging;

namespace ProjectHub.Application.Features.ProjectMembers.ListMembers;

/// <summary>
/// Query to list every member of a project. READ side of CQRS. <see cref="ProjectId"/> comes from the
/// ROUTE; the CALLER is resolved from the authenticated principal in the handler and must themselves be a
/// member to see the roster.
/// </summary>
/// <remarks>
/// WHY NOT PAGED?
/// A project's roster is a small, bounded set (tens, not thousands) and clients almost always render it in
/// full — a member dropdown, an avatar strip. Paging here would add ceremony with no payoff, so we return
/// the complete list in one shot. Contrast with comments/tasks, which are unbounded and therefore paged.
/// </remarks>
public sealed record ListMembersQuery(Guid ProjectId)
    : IQuery<IReadOnlyList<MemberResponse>>;
