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

public class OidcSessionResolverExtra2MutationCoverage : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public OidcSessionResolverExtra2MutationCoverage(PostgresFixture pg) => _pg = pg;

    private static async Task<(WaypointApiFactory factory, string cookieValue, Guid userId)> SeedSession(PostgresFixture pg, string[] groups)
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
            DisplayName = "Extra Two",
            Groups = groups,
        });
        db.UserSessions.Add(new UserSession
        {
            UserId = userId,
            CookieHash = OidcSessionResolver.HashCookie(cookieValue),
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(24),
        });
        await db.SaveChangesAsync();
        return (factory, cookieValue, userId);
    }

    [Fact]
    public async Task DeriveScopes_with_null_groups_returns_default_scopes_only()
    {
        var (factory, cookieValue, _) = await SeedSession(_pg, Array.Empty<string>());
        await using (factory)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WaypointDbContext>();
            var resolver = new OidcSessionResolver(db, Options.Create(new OidcOptions()));
            var ctx = new DefaultHttpContext();
            ctx.Request.Headers.Cookie = $"{OidcSessionResolver.CookieName}={cookieValue}";
            var p = await resolver.ResolveAsync(ctx, CancellationToken.None);
            p!.Scopes.Should().NotContain("admin");
            p.Scopes.Should().Contain("issue:read");
        }
    }

    [Fact]
    public async Task Custom_AdminGroups_in_options_is_what_matches_admin_scope()
    {
        // Kills mutations on AdminGroups membership check by using a custom group name.
        var (factory, cookieValue, _) = await SeedSession(_pg, new[] { "custom-admins" });
        await using (factory)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WaypointDbContext>();
            var customOpts = new OidcOptions { AdminGroups = new[] { "custom-admins" } };
            var resolver = new OidcSessionResolver(db, Options.Create(customOpts));
            var ctx = new DefaultHttpContext();
            ctx.Request.Headers.Cookie = $"{OidcSessionResolver.CookieName}={cookieValue}";
            var p = await resolver.ResolveAsync(ctx, CancellationToken.None);
            p!.Scopes.Should().Contain("admin");
        }
    }

    [Fact]
    public async Task User_in_unrelated_group_does_NOT_get_admin_scope()
    {
        var (factory, cookieValue, _) = await SeedSession(_pg, new[] { "developers", "qa" });
        await using (factory)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WaypointDbContext>();
            var resolver = new OidcSessionResolver(db, Options.Create(new OidcOptions()));
            var ctx = new DefaultHttpContext();
            ctx.Request.Headers.Cookie = $"{OidcSessionResolver.CookieName}={cookieValue}";
            var p = await resolver.ResolveAsync(ctx, CancellationToken.None);
            p!.Scopes.Should().NotContain("admin");
        }
    }
}
