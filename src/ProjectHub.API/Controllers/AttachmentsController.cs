using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectHub.Application.Features.Attachments.DeleteAttachment;
using ProjectHub.Application.Features.Attachments.DownloadAttachment;
using ProjectHub.Application.Features.Attachments.ListAttachments;
using ProjectHub.Application.Features.Attachments.UploadAttachment;

namespace ProjectHub.API.Controllers;

/// <summary>
/// The HTTP entry point for the attachment use cases. Like <see cref="CommentsController"/> every action is
/// a THIN adapter: it turns route/body/multipart into a command or query, dispatches through MediatR, and
/// hands the <c>Result</c> to <see cref="ApiController.HandleResult"/>. The ONE place it does more than the
/// comment controller is binding <c>multipart/form-data</c> for upload and streaming raw bytes back on
/// download — both are transport concerns that belong here, at the boundary, not in the Application layer.
/// </summary>
/// <remarks>
/// WHY TWO ROUTE SHAPES (SAME AS COMMENTS)?
/// An attachment is a CHILD of a task, so collection operations (list, upload) read as sub-resources of a
/// task: <c>/api/tasks/{taskId}/attachments</c> (absolute "~/..." templates escaping the controller prefix).
/// Once an attachment exists it has its own identity, so item operations (download, delete) hang off
/// <c>/api/attachments/{id}</c> and inherit the default prefix.
///
/// WHY IS THE STREAM PASSED AS A <c>Func&lt;Stream&gt;</c> ON THE COMMAND?
/// The command travels the whole MediatR pipeline before the handler runs; handing it a deferred factory
/// (<c>() =&gt; file.OpenReadStream()</c>) means no behavior can accidentally consume or dispose the live
/// request stream, and the handler opens it exactly once when it copies to storage. See the command's own
/// remarks for the full rationale.
///
/// Every action is <c>[Authorize]</c> (secure by default): attachments live inside private projects, so an
/// anonymous request has nothing it could legitimately touch and fails closed with 401.
/// </remarks>
[Authorize]
public sealed class AttachmentsController : ApiController
{
    private readonly ISender _sender;

    public AttachmentsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Uploads a file and attaches it to a task. The task id comes from the ROUTE; the bytes and their
    /// declared metadata arrive as <c>multipart/form-data</c> (an <see cref="IFormFile"/>); the uploader is
    /// resolved from the token in the handler (never from the client). Returns <c>201 Created</c> with the
    /// new attachment's id, echoed file name, and upload time, plus a <c>Location</c> header pointing at the
    /// task's attachment list.
    /// </summary>
    /// <remarks>
    /// WHY <c>IFormFile</c> AND NOT A RAW BODY?
    /// A browser/form posts files as multipart parts; <see cref="IFormFile"/> is ASP.NET's binding for one
    /// such part and gives us the declared file name, content type and length WITHOUT buffering the whole
    /// upload into memory — <c>OpenReadStream()</c> reads it lazily. We pass that opener (not the stream) so
    /// only the handler touches the bytes.
    /// </remarks>
    [HttpPost("~/api/tasks/{taskId:guid}/attachments")]
    [ProducesResponseType(typeof(UploadAttachmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Upload(
        Guid taskId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        // Guard the ONE thing model binding can't: a missing/empty part. Everything else (name/size/type
        // rules) is the validator's job; here we only ensure there is a file to open at all.
        if (file is null || file.Length == 0)
        {
            return BadRequest("A non-empty file is required.");
        }

        var command = new UploadAttachmentCommand(
            taskId,
            file.FileName,
            file.ContentType,
            file.Length,
            file.OpenReadStream);

        var result = await _sender.Send(command, cancellationToken);

        return HandleResult(result, value => CreatedAtAction(
            actionName: nameof(List),
            routeValues: new { taskId },
            value: value));
    }

    /// <summary>
    /// Lists a task's attachments, newest-first. The task id is bound from the route. Returns <c>200 OK</c>
    /// with the metadata rows only — no bytes, no storage paths.
    /// </summary>
    [HttpGet("~/api/tasks/{taskId:guid}/attachments")]
    [ProducesResponseType(typeof(IReadOnlyList<AttachmentListItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List(
        Guid taskId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ListAttachmentsQuery(taskId), cancellationToken);

        return HandleResult(result);
    }

    /// <summary>
    /// Downloads a single attachment's bytes. The attachment id comes from the route. On success the
    /// handler hands back an OPEN stream plus the original file name and content type; we stream it to the
    /// client with <c>File(...)</c>, which sets Content-Type / Content-Disposition and disposes the stream
    /// once the response is written.
    /// </summary>
    /// <remarks>
    /// WHY BYPASS <see cref="ApiController.HandleResult"/> ON SUCCESS?
    /// HandleResult serializes the success value as JSON — exactly wrong for a binary download. So we keep
    /// the railway pattern for the FAILURE branch (map the Error to a problem response) but on success emit
    /// a <c>FileStreamResult</c> instead of JSON. This is the one endpoint whose success shape is bytes.
    /// </remarks>
    [HttpGet("~/api/attachments/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new DownloadAttachmentQuery(id), cancellationToken);

        return HandleResult(result, value => File(
            value.Content,
            value.ContentType,
            value.FileName));
    }

    /// <summary>
    /// Deletes an attachment (metadata row AND its bytes). The attachment id comes from the route. Returns
    /// <c>204 No Content</c> on success, <c>403</c> if the caller may not delete it (not the uploader and
    /// not a Maintainer/Owner), or <c>404</c> if it is unknown/invisible.
    /// </summary>
    [HttpDelete("~/api/attachments/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new DeleteAttachmentCommand(id), cancellationToken);

        return HandleResult(result);
    }
}
