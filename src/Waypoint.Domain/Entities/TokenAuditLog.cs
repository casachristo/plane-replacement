using Waypoint.Domain.Enums;

namespace Waypoint.Domain.Entities;

public class TokenAuditLog
{
    public Guid Id { get; set; }
    public Guid TokenId { get; set; }
    public ApiToken Token { get; set; } = null!;
    /// <summary>WAY-5: tier of the token used for this call, denormalized so audit queries
    /// don't have to join ApiToken (which may be revoked) to know the tier.</summary>
    public TokenKind? TokenKind { get; set; }
    public string? PassthroughActorId { get; set; }
    public string? PassthroughActorLabel { get; set; }
    public required string Action { get; set; }
    public required string Path { get; set; }
    public required string Method { get; set; }
    public string? Ip { get; set; }
    public int StatusCode { get; set; }
    public DateTimeOffset At { get; set; }
}
