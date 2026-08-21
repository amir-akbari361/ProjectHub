// -------------------------------------------------------------------------------------------------
// Minimal JS interop helper for saving a file that was fetched by C# (Blazor Server).
//
// WHY THIS EXISTS
// In Interactive Server, C# runs on the server over a SignalR circuit — it cannot touch the browser's
// file system directly. The DownloadAttachment flow already fetches the bytes server-side WITH the JWT
// attached (so the endpoint stays authenticated), then hands them to this function via JS interop. This
// function turns those bytes into a transient object URL and clicks a hidden <a download> to trigger the
// browser's native "Save As". We deliberately do NOT expose the raw API URL to the browser: doing so
// would either leak an unauthenticated link or force the token into the query string. Round-tripping the
// bytes keeps the download strictly authenticated.
//
// The base64 argument is what Blazor produces when it marshals a byte[] across interop. We decode it to a
// Uint8Array, wrap it in a Blob with the server-provided content type, and revoke the object URL right
// after the click so we never leak memory for large or repeated downloads.
// -------------------------------------------------------------------------------------------------
window.projectHubDownload = {
  saveAs: function (fileName, contentType, base64) {
    // Decode base64 -> binary string -> byte array. atob is available in all evergreen browsers.
    const binary = atob(base64);
    const bytes = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i++) {
      bytes[i] = binary.charCodeAt(i);
    }

    const blob = new Blob([bytes], {
      type: contentType || "application/octet-stream",
    });
    const url = URL.createObjectURL(blob);

    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = fileName || "download";
    document.body.appendChild(anchor);
    anchor.click();

    // Clean up: detach the element and free the object URL so bytes are not retained.
    document.body.removeChild(anchor);
    URL.revokeObjectURL(url);
  },
};
