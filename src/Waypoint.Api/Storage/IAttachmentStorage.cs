namespace Waypoint.Api.Storage;

/// <summary>
/// Pluggable storage for binary attachment blobs. The attachments table already stores
/// metadata (filename, size, mime, storage_key) — this interface only handles bytes.
/// Two impls today: LocalFileAttachmentStorage (dev / single-pod) and a future
/// S3AttachmentStorage (MinIO / S3 / R2). DI registration picks one based on
/// Storage:Backend config — see Program.cs.
/// </summary>
public interface IAttachmentStorage
{
    /// <summary>Writes the stream to backing storage. Returns the storage_key for the row.</summary>
    Task<string> SaveAsync(Stream content, string filename, string mime, CancellationToken ct);

    /// <summary>Streams the blob back. Caller is responsible for disposing the returned stream.</summary>
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct);

    /// <summary>Removes the blob. Idempotent — does not throw if the key is unknown.</summary>
    Task DeleteAsync(string storageKey, CancellationToken ct);
}
