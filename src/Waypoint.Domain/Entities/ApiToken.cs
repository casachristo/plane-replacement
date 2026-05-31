using Waypoint.Domain.Enums;

namespace Waypoint.Domain.Entities;

public class ApiToken
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Prefix { get; set; }       // first 8 chars of secret for UI display
    public required string TokenHash { get; set; }    // argon2id hash of full secret
    public required string[] Scopes { get; set; }
    public TokenKind Kind { get; set; } = TokenKind.Service;
    public DateTimeOffset? LastUsedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
