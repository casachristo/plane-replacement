namespace Waypoint.Api.Subsystems.Identity.Scopes;

// The principals/scopes policy: maps a human's SSO groups to Waypoint scopes. Shared by every
// human principal resolver (OIDC session + Authelia header) and the Sessions service, so the
// grant is defined in exactly one place. Pure — no state, no I/O.
public static class ScopePolicy
{
    // Base scopes every authenticated human gets; admin is added iff they belong to an admin group.
    private static readonly string[] BaseHumanScopes =
        { "issue:read", "issue:create", "issue:write", "issue:transition", "comment:create" };

    public static IReadOnlyList<string> ForGroups(string[]? groups, IReadOnlyCollection<string> adminGroups)
    {
        var scopes = new List<string>(BaseHumanScopes);
        if (groups is not null && groups.Any(g => adminGroups.Contains(g, StringComparer.Ordinal)))
            scopes.Add("admin");
        return scopes;
    }
}
