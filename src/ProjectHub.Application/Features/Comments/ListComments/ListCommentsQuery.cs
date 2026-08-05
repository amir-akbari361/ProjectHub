using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Common;

namespace ProjectHub.Application.Features.Comments.ListComments;

/// <summary>
/// Query to page through the comment thread of a single task. READ side of CQRS. Carries the parent task
/// id (from the route) plus paging inputs, and returns a <see cref="PagedList{T}"/> of
/// <see cref="CommentResponse"/>. We page comments — an active task can accumulate hundreds — rather than
/// returning the whole thread in one payload.
/// </summary>
/// <remarks>
/// WHY DEFAULT VALUES ON THE RECORD?
/// A comment thread has a sensible default view (first page, 20 items, oldest-first) so the client can ask
/// for GET /tasks/{id}/comments with no query string. The validator still clamps these to safe bounds.
/// </remarks>
public sealed record ListCommentsQuery(
    Guid TaskId,
    int PageNumber = 1,
    int PageSize = 20)
    : IQuery<PagedList<CommentResponse>>;
