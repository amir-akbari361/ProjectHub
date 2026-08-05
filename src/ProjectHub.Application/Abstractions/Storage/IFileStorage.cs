namespace ProjectHub.Application.Abstractions.Storage;

/// <summary>
/// Port for binary blob storage. The Application layer uses this to persist and retrieve the RAW BYTES
/// of an attachment WITHOUT knowing whether they land on a local disk, an Azure Blob container, or an
/// S3 bucket. The concrete adapter lives in Infrastructure and is swapped via DI, so a handler never
/// changes when the storage backend does — the Ports and Adapters (Hexagonal) pattern, identical in
/// spirit to <c>IEmailSender</c> and <c>IJwtProvider</c>.
/// </summary>
/// <remarks>
/// WHY IS METADATA NOT IN HERE?
/// This port deals ONLY in bytes and an opaque storage key. File name, content type, size, uploader —
/// all of that is relational metadata owned by the <c>Attachment</c> aggregate and persisted in SQL.
/// Keeping the two concerns apart is exactly why the DB stays small and the blob store stays dumb.
///
/// WHY RETURN AN OPAQUE STRING KEY?
/// <see cref="SaveAsync"/> returns the storage key (e.g. a relative path or blob name) that the caller
/// stores on the aggregate as <c>StoragePath</c>. It is deliberately opaque: the local adapter may make
/// it a folder path, a cloud adapter a container/blob name. Callers must treat it as a token to hand
/// back to <see cref="OpenReadAsync"/> / <see cref="DeleteAsync"/>, never parse or build it themselves.
///
/// WHY STREAMS AND NOT byte[]?
/// A 25 MB upload materialized as a <c>byte[]</c> is 25 MB of Large Object Heap pressure PER request.
/// Streams let us copy from request body to backing store (and back on download) in bounded buffers, so
/// memory stays flat regardless of file size.
/// </remarks>
public interface IFileStorage
{
    /// <summary>
    /// Persists <paramref name="content"/> and returns the opaque storage key the caller must save on
    /// the aggregate. The adapter is responsible for generating a COLLISION-FREE key (two users may
    /// upload "report.pdf" to the same task); <paramref name="fileName"/> is a hint for extension/
    /// content-type preservation only, never a trusted path.
    /// </summary>
    Task<string> SaveAsync(Stream content, string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the stored blob identified by <paramref name="storagePath"/> for reading. Returns a stream
    /// the caller must dispose. Throws if the key does not resolve — a missing blob is an integrity
    /// error (DB row without its bytes), not an expected business outcome.
    /// </summary>
    Task<Stream> OpenReadAsync(string storagePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the stored blob identified by <paramref name="storagePath"/>. Idempotent: deleting an
    /// already-absent key is a no-op, so a retry after a partially-failed delete cannot throw.
    /// </summary>
    Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default);
}
