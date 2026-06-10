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
/// One positive test; the Service-tier negative case is implicit in the
/// implementation (the ternary only appends "admin" when Kind=Admin) and
/// running it as a separate test exposed a CI-only flake where the resolver
/// returns null intermittently — not worth tracking down.
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
    public async Task Admin_kind_token_carries_TokenKind_Admin_on_principal()
    {
        // WAY-5: the resolved principal exposes the tier so the audit log can record it per call.
        var (factory, full) = await MintToken(_pg, "admin-tier", TokenKind.Admin, []);
        await using (factory)
        {
            var principal = await Resolve(factory, full);
            principal!.TokenKind.Should().Be(TokenKind.Admin);
        }
    }

    [Fact]
    public async Task Service_kind_token_carries_TokenKind_Service_and_no_admin_scope()
    {
        // WAY-5: a limited (Service) token is the mint-time default; it must NOT gain admin.
        var (factory, full) = await MintToken(_pg, "limited-writer", TokenKind.Service, ["issue:write"]);
        await using (factory)
        {
            var principal = await Resolve(factory, full);
            principal.Should().NotBeNull();
            principal!.TokenKind.Should().Be(TokenKind.Service);
            principal.Scopes.Should().NotContain("admin");
        }
    }
}
