namespace Waypoint.Domain.Entities;

public class UserSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public required string CookieHash { get; set; }     // SHA-256(cookie value)
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public string? Ip { get; set; }
    public string? UserAgent { get; set; }
}
