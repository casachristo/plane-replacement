using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Waypoint.Api.Auth;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Domain;
using Waypoint.Domain.Entities;
using Xunit;

namespace Waypoint.Api.Tests.Auth;

/// <summary>
/// Mutation-coverage tests for OidcSessionResolver. The class is bypassed by the
/// WaypointApiFactory's FixedPrincipalResolver, so HTTP tests don't exercise it
/// at all — these drive the resolver directly with a cookie + a seeded session.
/// </summary>
public class OidcSessionResolverTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public OidcSessionResolverTests(PostgresFixture pg) => _pg = pg;

    private static OidcOptions DefaultOpts() => new();

    private static async Task<(WaypointApiFactory factory, string cookieValue, Guid userId)>
        SeedSession(PostgresFixture pg, string[]? groups = null, bool expired = false)
    {
        var factory = new WaypointApiFactory { PostgresConnectionString = pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        var cookieValue = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WaypointDbContext>();
        db.Users.Add(new User
        {
            Id = userId,
            Email = $"{userId}@waypoint.local",
            DisplayName = "Seeded User",
            Groups = groups ?? Array.Empty<string>(),
        });
        db.UserSessions.Add(new UserSession
        {
            UserId = userId,
            CookieHash = OidcSessionResolver.HashCookie(cookieValue),
            ExpiresAt = expired
                ? DateTimeOffset.UtcNow.AddHours(-1)
                : DateTimeOffset.UtcNow.AddHours(24),
        });
        await db.SaveChangesAsync();
        return (factory, cookieValue, userId);
    }

    private static async Task<Principal?> Resolve(WaypointApiFactory factory, string? cookieValue,
        OidcOptions? options = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WaypointDbContext>();
        var resolver = new OidcSessionResolver(db, Options.Create(options ?? DefaultOpts()));
        var ctx = new DefaultHttpContext();
        if (cookieValue is not null)
            ctx.Request.Headers.Cookie = $"{OidcSessionResolver.CookieName}={cookieValue}";
        return await resolver.ResolveAsync(ctx, CancellationToken.None);
    }

    [Fact]
    public async Task No_session_cookie_resolves_to_null()
    {
        var (factory, _, _) = await SeedSession(_pg);
        await using (factory)
        {
            var p = await Resolve(factory, cookieValue: null);
            p.Should().BeNull();
        }
    }

    [Fact]
    public async Task Cookie_with_no_matching_session_resolves_to_null()
    {
        var (factory, _, _) = await SeedSession(_pg);
        await using (factory)
        {
            var p = await Resolve(factory, "wrong-cookie-value");
            p.Should().BeNull();
        }
    }

    [Fact]
    public async Task Expired_session_resolves_to_null()
    {
        // Kills: ExpiresAt > UtcNow comparison flips.
        var (factory, cookieValue, _) = await SeedSession(_pg, expired: true);
        await using (factory)
        {
            var p = await Resolve(factory, cookieValue);
            p.Should().BeNull();
        }
    }

    [Fact]
    public async Task Valid_session_returns_Human_Principal_with_user_id_and_name()
    {
        var (factory, cookieValue, userId) = await SeedSession(_pg);
        await using (factory)
        {
            var p = await Resolve(factory, cookieValue);
            p.Should().NotBeNull();
            p!.Kind.Should().Be(PrincipalKind.Human);
            p.Id.Should().Be(userId.ToString());
            p.DisplayName.Should().Be("Seeded User");
        }
    }

    [Fact]
    public async Task Valid_session_default_scopes_DO_NOT_include_admin()
    {
        // Kills: DeriveScopes mutations that always-include or never-include admin.
        var (factory, cookieValue, _) = await SeedSession(_pg, groups: new[] { "engineers" });
        await using (factory)
        {
            var p = await Resolve(factory, cookieValue);
            p!.Scopes.Should().NotContain("admin");
        }
    }

    [Fact]
    public async Task User_in_AdminGroups_gets_admin_scope_synthesized()
    {
        // Kills: AdminGroups.Contains mutations.
        var (factory, cookieValue, _) = await SeedSession(_pg,
            groups: new[] { "waypoint-admins" });   // matches default OidcOptions.AdminGroups
        await using (factory)
        {
            var p = await Resolve(factory, cookieValue);
            p!.Scopes.Should().Contain("admin");
        }
    }

    [Fact]
    public async Task Default_scopes_include_issue_read_create_transition_and_comment_create()
    {
        var (factory, cookieValue, _) = await SeedSession(_pg);
        await using (factory)
        {
            var p = await Resolve(factory, cookieValue);
            p!.Scopes.Should().Contain("issue:read")
                .And.Contain("issue:create")
                .And.Contain("issue:transition")
                .And.Contain("comment:create");
        }
    }

    [Fact]
    public void HashCookie_is_stable_for_same_input()
    {
        OidcSessionResolver.HashCookie("seed-value")
            .Should().Be(OidcSessionResolver.HashCookie("seed-value"));
    }

    [Fact]
    public void HashCookie_changes_when_input_changes()
    {
        OidcSessionResolver.HashCookie("a")
            .Should().NotBe(OidcSessionResolver.HashCookie("b"));
    }

    [Fact]
    public void CookieName_constant_is_waypoint_session()
    {
        OidcSessionResolver.CookieName.Should().Be("waypoint_session");
    }
}
