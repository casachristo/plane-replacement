using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Waypoint.Api.Auth;
using Xunit;

namespace Waypoint.Api.Tests.Auth;

/// <summary>
/// Mutation-coverage tests for AutheliaHeaderResolver: the SSO header-trust path is gated by
/// TrustProxyHeaders, identity maps from Remote-Email/Name/Groups, and admin is group-derived.
/// </summary>
public class AutheliaHeaderResolverTests
{
    private static AutheliaHeaderResolver Make(bool trust, params string[] adminGroups) =>
        new(Options.Create(new OidcOptions
        {
            TrustProxyHeaders = trust,
            AdminGroups = adminGroups.Length == 0 ? new[] { "waypoint-admins" } : adminGroups,
        }));

    private static DefaultHttpContext Ctx(params (string, string)[] headers)
    {
        var ctx = new DefaultHttpContext();
        foreach (var (k, v) in headers) ctx.Request.Headers[k] = v;
        return ctx;
    }

    [Fact]
    public async Task Inert_when_trust_disabled_even_with_headers()
    {
        var p = await Make(trust: false).ResolveAsync(Ctx(("Remote-Email", "chris@casachristo.com")), default);
        p.Should().BeNull();
    }

    [Fact]
    public async Task Resolves_a_human_principal_from_remote_email_when_trusted()
    {
        var p = await Make(trust: true).ResolveAsync(
            Ctx(("Remote-Email", "chris@casachristo.com"), ("Remote-Name", "Chris")), default);
        p.Should().NotBeNull();
        p!.Kind.Should().Be(PrincipalKind.Human);
        p.Id.Should().Be("chris@casachristo.com");
        p.DisplayName.Should().Be("Chris");
    }

    [Fact]
    public async Task Display_name_falls_back_to_email_when_remote_name_absent()
    {
        var p = await Make(trust: true).ResolveAsync(Ctx(("Remote-Email", "  chris@casachristo.com  ")), default);
        p!.Id.Should().Be("chris@casachristo.com");        // trimmed
        p.DisplayName.Should().Be("chris@casachristo.com"); // falls back to (trimmed) email
    }

    [Fact]
    public async Task Null_when_no_remote_email_even_if_trusted()
    {
        (await Make(trust: true).ResolveAsync(Ctx(("Remote-Groups", "waypoint-admins")), default))
            .Should().BeNull();
        (await Make(trust: true).ResolveAsync(Ctx(("Remote-Email", "   ")), default))
            .Should().BeNull();
    }

    [Fact]
    public async Task Admin_scope_iff_user_is_in_an_admin_group()
    {
        var admin = await Make(trust: true, "waypoint-admins").ResolveAsync(
            Ctx(("Remote-Email", "a@b.c"), ("Remote-Groups", "users, waypoint-admins, dev")), default);
        admin!.Scopes.Should().Contain("admin");
        // base scopes always present
        admin.Scopes.Should().Contain(new[] { "issue:read", "issue:create", "issue:transition", "comment:create" });

        var nonAdmin = await Make(trust: true, "waypoint-admins").ResolveAsync(
            Ctx(("Remote-Email", "a@b.c"), ("Remote-Groups", "users, dev")), default);
        nonAdmin!.Scopes.Should().NotContain("admin");
    }

    [Fact]
    public void ParseGroups_splits_trims_and_drops_empties()
    {
        AutheliaHeaderResolver.ParseGroups("a, b ,,c").Should().Equal("a", "b", "c");
        AutheliaHeaderResolver.ParseGroups(null).Should().BeEmpty();
        AutheliaHeaderResolver.ParseGroups("   ").Should().BeEmpty();
    }
}
