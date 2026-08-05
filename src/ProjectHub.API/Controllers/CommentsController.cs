using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectHub.Application.Common;
using ProjectHub.Application.Features.Comments.AddComment;
using ProjectHub.Application.Features.Comments.EditComment;
using ProjectHub.Application.Features.Comments.ListComments;

namespace ProjectHub.API.Controllers;

/// <summary>
/// The HTTP entry point for the comment use cases. Like <see cref="TasksController"/> every action is a
/// THIN adapter: it stitches route/body into a command or query, dispatches through MediatR, and hands the
/// <c>Result</c> to <see cref="ApiController.HandleResult"/>. No business logic lives here — visibility,
/// role checks, and the "only the author may edit" invariant are enforced in the Application handlers and
/// the domain aggregate.
/// </summary>
/// <remarks>
/// WHY TWO ROUTE SHAPES?
/// A comment is a CHILD of a task, so the collection-level operations (list, add) read naturally as
/// sub-resources of a task: <c>/api/tasks/{taskId}/comments</c>. Once a comment exists it has its own
/// stable identity, so the item-level operation (edit) hangs off <c>/api/comments/{id}</c> — a client
/// holding a comment id shouldn't need its task to act on it. The task-scoped routes use absolute
/// templates ("~/...") to escape the controller's default <c>api/[controller]</c> prefix; the item route
/// inherits it.
///
/// Every action is <c>[Authorize]</c> (secure by default): comments are always posted BY a known member
/// and attributed to them, so a request without a valid token fails closed with 401.
/// </remarks>
[Authorize]
public sealed class CommentsController : ApiController
{
    private readonly ISender _sender;

    public CommentsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Posts a new comment onto a task. The task id comes from the ROUTE and the body from the request;
    /// the author is resolved from the token in the handler (never accepted from the client). Returns
    /// <c>201 Created</c> with the new comment's id and creation timestamp, and a <c>Location</c> header
    /// pointing at the task's comment thread (the natural place to observe the new comment).
    /// </summary>
    [HttpPost("~/api/tasks/{taskId:guid}/comments")]
    [ProducesResponseType(typeof(AddCommentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Add(
        Guid taskId,
        AddCommentRequest request,
        CancellationToken cancellationToken)
    {
        var command = new AddCommentCommand(taskId, request.Body);

        var result = await _sender.Send(command, cancellationToken);

        return HandleResult(result, value => CreatedAtAction(
            actionName: nameof(List),
            routeValues: new { taskId },
            value: value));
    }

    /// <summary>
    /// Lists a task's comment thread as a single page, oldest-first. Paging arrives as QUERY-STRING
    /// parameters; the task id is bound from the route and combined into the full
    /// <see cref="ListCommentsQuery"/>. Returns <c>200 OK</c> with a <see cref="PagedList{T}"/> envelope.
    /// </summary>
    [HttpGet("~/api/tasks/{taskId:guid}/comments")]
    [ProducesResponseType(typeof(PagedList<CommentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List(
        Guid taskId,
        [FromQuery] ListCommentsRequest request,
        CancellationToken cancellationToken)
    {
        var query = new ListCommentsQuery(taskId, request.PageNumber, request.PageSize);

        var result = await _sender.Send(query, cancellationToken);

        return HandleResult(result);
    }

    /// <summary>
    /// Edits the body of an existing comment. The comment id comes from the route and the new body from
    /// the request. Returns <c>204 No Content</c> on success, <c>403</c> if the caller is not the author,
    /// or <c>404</c> if the comment is unknown/invisible.
    /// </summary>
    /// <remarks>
    /// WHY <c>PUT</c> HERE? Editing a comment fully replaces its body — the mutable representation of the
    /// resource — so PUT (idempotent, whole-representation replacement) is the precise verb, unlike the
    /// named verb-sub-resources used for task assignment/status.
    /// </remarks>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Edit(
        Guid id,
        EditCommentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new EditCommentCommand(id, request.Body), cancellationToken);

        return HandleResult(result);
    }
}

/// <summary>
/// Request body for <see cref="CommentsController.Add"/>. Carries ONLY the body; the parent task id is
/// owned by the route and the author by the token, so neither can be spoofed via the body.
/// </summary>
public sealed record AddCommentRequest(string Body);

/// <summary>
/// Query-string binding target for <see cref="CommentsController.List"/>. Mirrors the paging concerns of
/// <c>ListCommentsQuery</c> WITHOUT the task id (a route value). Defaults match the query's own defaults
/// so a bare <c>GET</c> is valid.
/// </summary>
public sealed record ListCommentsRequest(
    int PageNumber = 1,
    int PageSize = 20);

/// <summary>Request body for <see cref="CommentsController.Edit"/>. The comment id is owned by the route.</summary>
public sealed record EditCommentRequest(string Body);
