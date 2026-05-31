namespace Waypoint.Domain.Entities;

public class TokenAuditLog
{
    public Guid Id { get; set; }
    public Guid TokenId { get; set; }
    public ApiToken Token { get; set; } = null!;
    public string? PassthroughActorId { get; set; }
    public string? PassthroughActorLabel { get; set; }
    public required string Action { get; set; }
    public required string Path { get; set; }
    public required string Method { get; set; }
    public string? Ip { get; set; }
    public int StatusCode { get; set; }
    public DateTimeOffset At { get; set; }
}
