using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Waypoint.Api.Auth;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Domain;
using Waypoint.Domain.Entities;
using Waypoint.Domain.Enums;
using Xunit;

namespace Waypoint.Api.Tests.Auth;

/// <summary>
/// WAY-5: Admin-tier API tokens get a synthetic "admin" scope at resolve time.
/// SurfaceGuardMiddleware blocks Bearer tokens on the public surface, so we can't
/// exercise the full admin endpoint path via HTTP — instead this drives
/// ServiceBearerResolver directly and asserts on the resolved Principal.
/// </summary>
public class AdminTokenTierTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public AdminTokenTierTests(PostgresFixture pg) => _pg = pg;

    private static async Task<(WaypointApiFactory factory, string fullToken)>
        MintToken(PostgresFixture pg, string name, TokenKind kind, string[] scopes)
    {
        var factory = new WaypointApiFactory { PostgresConnectionString = pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        var (prefix, full) = TokenHasher.GenerateNew();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WaypointDbContext>();
        db.ApiTokens.Add(new ApiToken
        {
            Name = name,
            Prefix = prefix,
            TokenHash = TokenHasher.Hash(full),
            Scopes = scopes,
            Kind = kind,
        });
        await db.SaveChangesAsync();
        return (factory, full);
    }

    private static async Task<Principal?> Resolve(WaypointApiFactory factory, string bearer)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WaypointDbContext>();
        var resolver = new ServiceBearerResolver(db);
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers.Authorization = $"Bearer {bearer}";
        return await resolver.ResolveAsync(ctx, CancellationToken.None);
    }

    [Fact]
    public async Task Admin_kind_token_gets_synthetic_admin_scope()
    {
        var (factory, full) = await MintToken(_pg, "admin-bootstrap", TokenKind.Admin, []);
        await using (factory)
        {
            var principal = await Resolve(factory, full);
            principal.Should().NotBeNull();
            principal!.Scopes.Should().Contain("admin");
            principal.Kind.Should().Be(PrincipalKind.InternalService);
        }
    }

    [Fact]
    public async Task Service_kind_token_does_NOT_get_admin_scope_synthesized()
    {
        var (factory, full) = await MintToken(_pg, "agent-1", TokenKind.Service, ["read", "write"]);
        await using (factory)
        {
            var principal = await Resolve(factory, full);
            principal.Should().NotBeNull();
            principal!.Scopes.Should().NotContain("admin");
            principal.Scopes.Should().BeEquivalentTo(new[] { "read", "write" });
        }
    }

}
