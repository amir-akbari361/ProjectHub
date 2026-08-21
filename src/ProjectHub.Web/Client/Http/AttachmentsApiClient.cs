using System.Net.Http.Headers;
using System.Net.Http.Json;
using ProjectHub.Web.Client.Models;

namespace ProjectHub.Web.Client.Http;

/// <summary>
/// Typed HTTP client for task attachments: list, upload, download, delete. Backs the attachments panel on
/// the task detail page. Like every other client it leans on <see cref="HttpResultExtensions"/> for the
/// JSON endpoints so success/error parsing stays uniform — but the DOWNLOAD path is deliberately different:
/// the server streams raw bytes there, not a JSON envelope, so this client exposes the bytes + file name
/// through a small <see cref="FileDownload"/> record instead of forcing them through the JSON helper.
/// </summary>
/// <remarks>
/// WHY TWO ROUTE SHAPES (MIRRORS THE API)?
/// Collection operations are sub-resources of a task — <c>api/tasks/{taskId}/attachments</c> — because an
/// attachment is a CHILD of a task. Once it exists it has its own identity, so item operations key off
/// <c>api/attachments/{id}</c>. The client mirrors the controller exactly so there is a single source of
/// truth for the URL space.
/// </remarks>
public sealed class AttachmentsApiClient
{
    private readonly HttpClient _http;

    public AttachmentsApiClient(HttpClient http)
    {
        _http = http;
    }

    /// <summary>
    /// Lists a task's attachments (metadata only). Mirrors <c>GET api/tasks/{taskId}/attachments</c>, whose
    /// success shape is a bare JSON array — so we deserialize into a <see cref="List{T}"/>, NOT a paged
    /// envelope. The attachment list is intentionally un-paged on the server; keeping the client shape
    /// identical avoids the silent empty-list bug a shape mismatch would cause.
    /// </summary>
    public async Task<ApiResult<List<AttachmentItem>>> ListAsync(Guid taskId)
    {
        var response = await _http.GetAsync($"api/tasks/{taskId}/attachments");
        return await response.ToResultAsync<List<AttachmentItem>>();
    }

    /// <summary>
    /// Uploads a file to a task as <c>multipart/form-data</c>. The API binds an <see cref="IFormFile"/>
    /// named <c>file</c>, so the form field name here MUST be "file" or model binding yields nothing and the
    /// controller's empty-file guard returns 400. We stream the browser file's content straight into the
    /// multipart part (no full in-memory buffer) and copy its content type so the server records it faithfully.
    /// </summary>
    public async Task<ApiResult<UploadAttachmentResult>> UploadAsync(
        Guid taskId,
        Stream content,
        string fileName,
        string contentType)
    {
        // 'using' disposes the multipart content (and the stream/part content it owns) once the request has
        // been sent, so we never leak the browser file handle even if the call throws.
        using var form = new MultipartFormDataContent();
        var fileContent = new StreamContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);

        // The three arguments are: the part content, the FORM FIELD NAME (must be "file"), and the file name
        // the server should record. Getting the field name wrong is the single most common upload bug.
        form.Add(fileContent, "file", fileName);

        var response = await _http.PostAsync($"api/tasks/{taskId}/attachments", form);
        return await response.ToResultAsync<UploadAttachmentResult>();
    }

    /// <summary>
    /// Downloads a single attachment's bytes from <c>GET api/attachments/{id}</c>. This is the ONE endpoint
    /// whose success body is binary, not JSON, so we bypass <see cref="HttpResultExtensions"/> and read the
    /// raw byte array plus the file name from the <c>Content-Disposition</c> header. Returning a
    /// <see cref="FileDownload"/> lets the page hand the bytes to a JS <c>saveAs</c> without ever exposing a
    /// naked, un-authenticated URL (the token still travels via the Bearer handler on this request).
    /// </summary>
    public async Task<ApiResult<FileDownload>> DownloadAsync(Guid attachmentId)
    {
        var response = await _http.GetAsync(
            $"api/attachments/{attachmentId}", HttpCompletionOption.ResponseHeadersRead);

        if (!response.IsSuccessStatusCode)
        {
            // Reuse the shared problem-details parsing for the failure branch so error messages read the
            // same as every other client, even though the success branch is bespoke.
            var failure = await response.ToResultAsync<FileDownload>();
            return failure;
        }

        var bytes = await response.Content.ReadAsByteArrayAsync();
        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
            ?? "download";
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";

        return ApiResult<FileDownload>.Success(new FileDownload(fileName, contentType, bytes));
    }

    /// <summary>
    /// Deletes an attachment (metadata row AND bytes) via <c>DELETE api/attachments/{id}</c>. Keyed by the
    /// attachment's own id because deletion is an item-level operation; the caller needs no task id.
    /// </summary>
    public async Task<ApiResult> DeleteAsync(Guid attachmentId)
    {
        var response = await _http.DeleteAsync($"api/attachments/{attachmentId}");
        return await response.ToResultAsync();
    }
}

/// <summary>
/// The client-side shape of a downloaded file: its name, content type, and raw bytes. This is NOT a wire
/// contract (nothing is serialized to/from JSON) — it's an in-memory carrier so the page can pass the bytes
/// to a JS <c>saveAs</c> helper. Kept next to the client that produces it since it has no other consumer.
/// </summary>
public sealed record FileDownload(string FileName, string ContentType, byte[] Content);
