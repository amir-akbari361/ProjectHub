namespace ProjectHub.Application.Features.ProjectMembers.AddMember;

/// <summary>
/// The result returned after a user is added to a project. A lean write-side acknowledgement — the new
/// membership row's id — so the client can address the member (e.g. for a follow-up role change or
/// removal) without a second round-trip. We deliberately do NOT echo the whole member/user aggregate:
/// the READ model belongs to the query side (ListMembers).
/// </summary>
public sealed record AddMemberResponse(Guid MemberId);
