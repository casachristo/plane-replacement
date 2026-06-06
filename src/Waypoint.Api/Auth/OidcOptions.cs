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

    // When true, Waypoint trusts the Remote-Email / Remote-Name / Remote-Groups identity
    // headers injected by an Authelia forward-auth middleware on the ingress, so a homelab
    // SSO'd user is recognized WITHOUT a separate Waypoint OIDC login (matches how Cairn
    // trusts Remote-Email behind Authelia). MUST stay false until the ingress is Authelia-
    // gated AND the public API port only accepts proxied traffic — otherwise a spoofed
    // header could grant access. Default false (inert).
    public bool TrustProxyHeaders { get; set; }
}
