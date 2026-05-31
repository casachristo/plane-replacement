namespace Waypoint.Api.Auth;

/// <summary>
/// OIDC client configuration. Bound from appsettings under "Auth:Oidc". To swap providers
/// (Keycloak, Auth0, etc.), change config values only — no code change required.
/// </summary>
public sealed class OidcOptions
{
    public const string SectionName = "Auth:Oidc";

    public string Authority { get; set; } = "https://auth.chris.box";
    public string ClientId { get; set; } = "waypoint";
    public string ClientSecret { get; set; } = "";
    public string RedirectUri { get; set; } = "https://waypoint.chris.box/auth/callback";
    public string[] Scopes { get; set; } = ["openid", "profile", "email", "groups"];
    public string EmailClaim { get; set; } = "email";
    public string NameClaim { get; set; } = "name";
    public string GroupsClaim { get; set; } = "groups";
    public string[] AdminGroups { get; set; } = ["waypoint-admins"];
    public TimeSpan SessionLifetime { get; set; } = TimeSpan.FromDays(30);
}
