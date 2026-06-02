namespace Waypoint.Api.Storage;

/// <summary>
/// Filesystem-backed implementation. Fine for single-pod homelab use; for multi-replica
/// or HA you want S3/MinIO instead. Storage key format: "yyyy/MM/dd/{guid}-{filename}".
/// Filename is sanitized to keep `..` and path separators out.
/// </summary>
public sealed class LocalFileAttachmentStorage : IAttachmentStorage
{
    private readonly string _root;

    public LocalFileAttachmentStorage(IConfiguration config)
    {
        _root = config.GetValue<string>("Storage:Local:Root") ?? "/var/lib/waypoint/attachments";
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(Stream content, string filename, string mime, CancellationToken ct)
    {
        var safeName = string.Concat(filename.Where(c => !Path.GetInvalidFileNameChars().Contains(c)));
        if (string.IsNullOrWhiteSpace(safeName)) safeName = "unnamed";
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
        var absolute = Path.Combine(_root, storageKey);
        if (!File.Exists(absolute)) throw new FileNotFoundException($"Attachment not found: {storageKey}", absolute);
        return Task.FromResult<Stream>(File.OpenRead(absolute));
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct)
    {
        var absolute = Path.Combine(_root, storageKey);
        if (File.Exists(absolute)) File.Delete(absolute);
        return Task.CompletedTask;
    }
}
