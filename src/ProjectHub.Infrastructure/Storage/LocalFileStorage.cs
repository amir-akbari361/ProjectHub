using Microsoft.Extensions.Options;
using ProjectHub.Application.Abstractions.Storage;

namespace ProjectHub.Infrastructure.Storage;

/// <summary>
/// The local-disk ADAPTER for the <see cref="IFileStorage"/> port. It persists uploaded blobs under a
/// configured root folder and hands back an opaque storage key. Swapping this for an Azure Blob / S3
/// adapter later is a one-line DI change — no handler, controller, or aggregate is touched. That is the
/// whole point of the Ports and Adapters pattern.
/// </summary>
/// <remarks>
/// WHY <c>internal</c>?
/// Callers depend on the PORT (<see cref="IFileStorage"/>), never this concrete type. Keeping it internal
/// makes that impossible to violate and lets us change the implementation freely — the same discipline
/// applied to <c>JwtProvider</c>/<c>SmtpEmailSender</c>.
///
/// KEY SHAPE: we bucket blobs by a two-level fan-out of the generated GUID
/// (<c>{aa}/{bb}/{guid}{ext}</c>). A single flat directory with tens of thousands of files degrades on
/// most filesystems; the two nibble-prefix folders keep any one directory small. The key we RETURN is a
/// relative, forward-slash path so it is portable across OSes and stored verbatim on the aggregate as
/// <c>StoragePath</c> — opaque to every caller, exactly as the port documents.
///
/// SECURITY: the client's file name is NEVER used to build a path (it could contain <c>..\</c> traversal).
/// We generate our own GUID name and only preserve the (sanitised) extension for content-type fidelity.
/// </remarks>
internal sealed class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IOptions<FileStorageOptions> options)
    {
        var configured = options.Value.RootPath;

        // Resolve a relative root against the app base directory so the same config works under
        // `dotnet run` and in a published/container layout. An absolute path is honoured as-is.
        _root = Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(AppContext.BaseDirectory, configured);

        // Ensure the root exists once, at construction (singleton), rather than on every save. Idempotent.
        Directory.CreateDirectory(_root);
    }

    /// <summary>
    /// Streams <paramref name="content"/> to a freshly-generated, collision-free path and returns the
    /// relative storage key. We copy in bounded buffers (<c>CopyToAsync</c>) so a 25 MB upload never
    /// materialises as a single array — memory stays flat regardless of file size.
    /// </summary>
    public async Task<string> SaveAsync(
        Stream content,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        // Preserve ONLY the extension from the untrusted name (for content-type fidelity on download);
        // the body of the name is a server-generated GUID so two "report.pdf" uploads never collide and
        // no client string ever influences the on-disk path.
        var extension = Path.GetExtension(fileName);
        var id = Guid.NewGuid().ToString("N");

        // Two-level nibble fan-out keeps any single directory small. The RELATIVE key (forward slashes)
        // is what we persist and later hand back to OpenRead/Delete.
        var relativeKey = $"{id[..2]}/{id[2..4]}/{id}{extension}";
        var absolutePath = ToAbsolute(relativeKey);

        // Create the two bucket folders if this is their first blob. Idempotent and cheap.
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

        // FileMode.CreateNew guarantees we never silently overwrite an existing blob — with a fresh GUID a
        // collision is astronomically unlikely, so if it ever happened it would be a real bug worth failing on.
        await using var destination = new FileStream(
            absolutePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        await content.CopyToAsync(destination, cancellationToken);

        return relativeKey;
    }

    /// <summary>
    /// Opens the blob identified by <paramref name="storagePath"/> for reading. Throws
    /// <see cref="FileNotFoundException"/> if the key does not resolve — a DB row without its bytes is an
    /// integrity error (an unexpected exception), NOT a modelled business outcome.
    /// </summary>
    public Task<Stream> OpenReadAsync(
        string storagePath,
        CancellationToken cancellationToken = default)
    {
        var absolutePath = ToAbsolute(storagePath);

        if (!File.Exists(absolutePath))
        {
            throw new FileNotFoundException(
                $"Stored blob not found for key '{storagePath}'.", absolutePath);
        }

        Stream stream = new FileStream(
            absolutePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);

        // The interface is async (a cloud adapter awaits a network open); the local open is synchronous,
        // so we wrap the ready stream in a completed task rather than fake asynchrony.
        return Task.FromResult(stream);
    }

    /// <summary>
    /// Deletes the blob identified by <paramref name="storagePath"/>. Idempotent: <see cref="File.Delete"/>
    /// is a no-op on an already-absent file, so a retry after a partial failure cannot throw — exactly the
    /// contract the port promises.
    /// </summary>
    public Task DeleteAsync(
        string storagePath,
        CancellationToken cancellationToken = default)
    {
        File.Delete(ToAbsolute(storagePath));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Resolves a stored relative key back to a full path UNDER the root, and defends against traversal:
    /// even though we generate keys ourselves, a corrupted/hand-edited value containing <c>..</c> must
    /// never escape the root. We canonicalise and assert the result stays inside <c>_root</c>.
    /// </summary>
    private string ToAbsolute(string relativeKey)
    {
        // Normalise the forward-slash key to the OS separator, then combine + canonicalise.
        var combined = Path.GetFullPath(
            Path.Combine(_root, relativeKey.Replace('/', Path.DirectorySeparatorChar)));

        // Confinement check: the canonical path must sit within the root. Anything else is tampering.
        var rootWithSeparator = _root.EndsWith(Path.DirectorySeparatorChar)
            ? _root
            : _root + Path.DirectorySeparatorChar;

        if (!combined.StartsWith(rootWithSeparator, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Resolved storage path '{combined}' escapes the storage root.");
        }

        return combined;
    }
}
