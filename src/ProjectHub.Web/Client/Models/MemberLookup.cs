namespace ProjectHub.Web.Client.Models;

/// <summary>
/// Resolves the user ids that read models carry into names and initials, using a project's member roster.
/// </summary>
/// <remarks>
/// WHY THIS EXISTS
/// The API's task, comment and attachment projections identify people by <see cref="Guid"/> only — deliberately,
/// so a read model does not have to join user rows it may not need. The roster is fetched once per project page,
/// and every component that renders a person (the task list's assignee, the board card's avatar, the comment
/// thread's author, the attachment's uploader) needs the same id→name resolution. Each was doing its own
/// <c>Members.FirstOrDefault(...)</c> with its own fallback string, so a user who had left the project showed as
/// "Former member" in one place and "Unassigned" in another for the same id.
///
/// WHY A DICTIONARY RATHER THAN SCANNING THE LIST
/// A board can render a few hundred cards, each resolving an assignee. A linear scan per card is O(cards × members)
/// on every re-render, and re-renders happen on every drag. Building the index once per roster load makes each
/// lookup O(1) — a small change that matters precisely because it sits inside a render loop.
///
/// WHY IT IS A CLASS AND NOT EXTENSION METHODS ON THE LIST
/// The index has to be built somewhere. Extension methods would rebuild it on every call, which is the problem
/// they would be trying to solve.
/// </remarks>
public sealed class MemberLookup
{
    /// <summary>Shown for a null or empty assignee id — the task has nobody on it.</summary>
    public const string UnassignedLabel = "Unassigned";

    /// <summary>
    /// Shown for an id that is not on the current roster. This is a real, expected case: someone can comment on a
    /// task and later be removed from the project, and their comment must still render an author.
    /// </summary>
    public const string FormerMemberLabel = "Former member";

    private readonly Dictionary<Guid, MemberItem> _byUserId;

    private MemberLookup(Dictionary<Guid, MemberItem> byUserId)
    {
        _byUserId = byUserId;
    }

    /// <summary>An empty lookup, for use before the roster has loaded. Avoids a null check at every call site.</summary>
    public static MemberLookup Empty { get; } = new(new Dictionary<Guid, MemberItem>());

    /// <summary>Indexes a roster. A null or empty roster yields <see cref="Empty"/>-equivalent behaviour.</summary>
    public static MemberLookup From(IReadOnlyList<MemberItem>? members)
    {
        if (members is null || members.Count == 0)
        {
            return Empty;
        }

        // A roster cannot contain the same user twice (the server enforces one membership per user per project),
        // but building with an explicit overwrite rather than ToDictionary means a duplicate from a future API
        // change degrades to "last wins" instead of throwing inside a render pass.
        var index = new Dictionary<Guid, MemberItem>(members.Count);

        foreach (var member in members)
        {
            index[member.UserId] = member;
        }

        return new MemberLookup(index);
    }

    /// <summary>The member with this id, or null if they are not on the roster.</summary>
    public MemberItem? Find(Guid userId) =>
        _byUserId.TryGetValue(userId, out var member) ? member : null;

    /// <summary>
    /// A display name for a possibly-absent assignee: the member's full name, <see cref="UnassignedLabel"/> for
    /// no id, or <see cref="FormerMemberLabel"/> for an id no longer on the roster.
    /// </summary>
    public string DisplayName(Guid? userId)
    {
        if (userId is null || userId.Value == Guid.Empty)
        {
            return UnassignedLabel;
        }

        return Find(userId.Value)?.FullName ?? FormerMemberLabel;
    }

    /// <summary>The single uppercase initial used on an avatar, derived from the same name as above.</summary>
    public string Initial(Guid? userId)
    {
        var name = DisplayName(userId);

        return string.IsNullOrWhiteSpace(name)
            ? "?"
            : char.ToUpperInvariant(name[0]).ToString();
    }

    /// <summary>
    /// True when the user holds a role that may manage the project (Maintainer or Owner). Used to decide which
    /// controls to OFFER; the server re-checks every mutation, so this only avoids presenting an action that is
    /// certain to come back 403.
    /// </summary>
    public bool CanManage(Guid? userId)
    {
        if (userId is null)
        {
            return false;
        }

        var member = Find(userId.Value);

        return member is not null
            && member.Role is Domain.Enums.ProjectRole.Maintainer or Domain.Enums.ProjectRole.Owner;
    }

    /// <summary>True when the user is the project's Owner — the tier that may archive it and manage owners.</summary>
    public bool IsOwner(Guid? userId) =>
        userId is not null && Find(userId.Value)?.Role == Domain.Enums.ProjectRole.Owner;

    /// <summary>
    /// True when the user may create and mutate tasks (Contributor and above). Viewers get a read-only board.
    /// </summary>
    public bool CanContribute(Guid? userId)
    {
        if (userId is null)
        {
            return false;
        }

        var member = Find(userId.Value);

        return member is not null && member.Role >= Domain.Enums.ProjectRole.Contributor;
    }
}
