namespace Waypoint.Api.Storage;

/// <summary>
/// Filesystem-backed implementation. Fine for single-pod homelab use; for multi-replica
/// or HA you want S3/MinIO instead. Storage key format: "yyyy/MM/dd/{guid}-{filename}".
/// Filename is sanitized to keep path separators out; ResolveSafe enforces that any
/// storage_key read from the DB stays under _root (defense against a tampered row).
/// </summary>
public sealed class LocalFileAttachmentStorage : IAttachmentStorage
{
    private readonly string _root;
    private readonly string _rootFull;     // canonical (resolved) form for path-containment checks

    public LocalFileAttachmentStorage(IConfiguration config)
    {
        _root = config.GetValue<string>("Storage:Local:Root") ?? "/var/lib/waypoint/attachments";
        Directory.CreateDirectory(_root);
        _rootFull = Path.GetFullPath(_root) + Path.DirectorySeparatorChar;
    }

    public async Task<string> SaveAsync(Stream content, string filename, string mime, CancellationToken ct)
    {
        var safeName = string.Concat(filename.Where(c => !Path.GetInvalidFileNameChars().Contains(c)));
        if (string.IsNullOrWhiteSpace(safeName) || safeName == "." || safeName == "..") safeName = "unnamed";
        var now = DateTimeOffset.UtcNow;
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        var relative = Path.Combine(now.Year.ToString(ci), now.Month.ToString("D2", ci), now.Day.ToString("D2", ci), $"{Guid.NewGuid():N}-{safeName}");
        var absolute = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        await using var fs = File.Create(absolute);
        await content.CopyToAsync(fs, ct);
        return relative.Replace('\\', '/');
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct)
    {
        var absolute = ResolveSafe(storageKey);
        if (!File.Exists(absolute)) throw new FileNotFoundException($"Attachment not found: {storageKey}", absolute);
        return Task.FromResult<Stream>(File.OpenRead(absolute));
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct)
    {
        var absolute = ResolveSafe(storageKey);
        if (File.Exists(absolute)) File.Delete(absolute);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Joins storage_key under _root and confirms the canonical result is still inside _root.
    /// Defends against path traversal via `..` segments in a tampered DB row — Path.GetInvalidFileNameChars
    /// does NOT include the two-dot sequence, so the SaveAsync sanitization alone isn't enough
    /// for the read/delete paths.
    /// </summary>
    private string ResolveSafe(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
            throw new ArgumentException("Storage key required.", nameof(storageKey));
        var candidate = Path.GetFullPath(Path.Combine(_root, storageKey));
        if (!candidate.StartsWith(_rootFull, StringComparison.Ordinal))
            throw new UnauthorizedAccessException($"Storage key '{storageKey}' resolves outside the attachment root.");
        return candidate;
    }
}
