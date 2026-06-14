using Microsoft.Extensions.Options;
using Waypoint.Api.Subsystems.Identity.Scopes;

namespace Waypoint.Api.Auth;

/// <summary>
/// Resolves a human Principal from the identity headers an Authelia forward-auth middleware
/// injects on the ingress (Remote-Email / Remote-Name / Remote-Groups). This lets a user who
/// is already signed in to the homelab SSO be recognized by Waypoint WITHOUT a separate
/// Waypoint OIDC login — the same pattern Cairn uses (trusting Remote-Email behind Authelia).
///
/// SAFETY: trusting proxy headers is only sound when (a) the ingress runs Authelia forward-auth
/// so the headers are set by a trusted proxy, and (b) the public API port only accepts proxied
/// traffic. Until both hold, this resolver is INERT — it returns null unless
/// <see cref="OidcOptions.TrustProxyHeaders"/> is explicitly enabled in config. So a spoofed
/// header cannot grant access in the default deployment.
/// </summary>
public sealed class AutheliaHeaderResolver : IPrincipalResolver
{
    private readonly OidcOptions _options;

    public AutheliaHeaderResolver(IOptions<OidcOptions> options) => _options = options.Value;

    public Task<Principal?> ResolveAsync(HttpContext ctx, CancellationToken ct)
    {
        if (!_options.TrustProxyHeaders) return Task.FromResult<Principal?>(null);

        var email = Header(ctx, "Remote-Email");
        if (string.IsNullOrWhiteSpace(email)) return Task.FromResult<Principal?>(null);
        email = email.Trim();

        var name = Header(ctx, "Remote-Name");
        var groups = ParseGroups(Header(ctx, "Remote-Groups"));

        var principal = new Principal(
            Kind: PrincipalKind.Human,
            Id: email,
            DisplayName: string.IsNullOrWhiteSpace(name) ? email : name.Trim(),
            Scopes: ScopePolicy.ForGroups(groups, _options.AdminGroups));
        return Task.FromResult<Principal?>(principal);
    }

    private static string? Header(HttpContext ctx, string name) =>
        ctx.Request.Headers.TryGetValue(name, out var v) ? v.ToString() : null;

    public static string[] ParseGroups(string? raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? Array.Empty<string>()
            : raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}
