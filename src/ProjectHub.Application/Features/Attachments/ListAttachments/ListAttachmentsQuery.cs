using ProjectHub.Application.Abstractions.Messaging;

namespace ProjectHub.Application.Features.Attachments.ListAttachments;

/// <summary>
/// Query for all attachments on a task, newest first. A READ-side request returning lightweight metadata
/// rows (<see cref="AttachmentListItemResponse"/>), never the bytes. Attachment lists per task are
/// naturally small and bounded, so — like the comment list — we return the full set rather than a paged
/// slice; if a task ever accumulates hundreds of files this would gain a <c>PagedList</c> like the project
/// and task lists, but paging an unbounded-in-theory list that is small in practice is premature here.
/// </summary>
public sealed record ListAttachmentsQuery(Guid TaskId)
    : IQuery<IReadOnlyList<AttachmentListItemResponse>>;
