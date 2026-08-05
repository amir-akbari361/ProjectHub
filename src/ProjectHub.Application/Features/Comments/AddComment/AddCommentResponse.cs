namespace ProjectHub.Application.Features.Comments.AddComment;

/// <summary>
/// The result returned after a comment is posted. A lean write-side acknowledgement — just the new
/// comment's id plus the server-stamped creation timestamp — so the client can optimistically render
/// the new comment (and address it via the item route) without a second round-trip. We deliberately do
/// NOT echo the whole comment aggregate: the READ model belongs to the query side.
/// </summary>
public sealed record AddCommentResponse(
    Guid Id,
    DateTime CreatedAtUtc);
