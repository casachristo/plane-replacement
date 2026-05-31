namespace Waypoint.Domain.Entities;

public class Attachment
{
    public Guid Id { get; set; }
    public Guid? IssueId { get; set; }
    public Issue? Issue { get; set; }
    public Guid? CommentId { get; set; }
    public Comment? Comment { get; set; }
    public required string Filename { get; set; }
    public long Size { get; set; }
    public required string Mime { get; set; }
    public required string StorageKey { get; set; }
    public Guid? UploadedByUserId { get; set; }
    public User? UploadedByUser { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
