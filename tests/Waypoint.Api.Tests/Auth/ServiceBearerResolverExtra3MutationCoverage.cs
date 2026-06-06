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

public class ServiceBearerResolverExtra3MutationCoverage : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public ServiceBearerResolverExtra3MutationCoverage(PostgresFixture pg) => _pg = pg;

    private async Task<(WaypointApiFactory factory, string full)> Mint(TokenKind kind, string[] scopes)
    {
        var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        var (prefix, full) = TokenHasher.GenerateNew();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WaypointDbContext>();
        db.ApiTokens.Add(new ApiToken
        {
            Name = "extra3-" + prefix,
            Prefix = prefix,
            TokenHash = TokenHasher.Hash(full),
            Scopes = scopes,
            Kind = kind,
        });
        await db.SaveChangesAsync();
        return (factory, full);
    }

    [Fact]
    public async Task Passthrough_actor_label_alone_without_id_does_not_set_actor_id()
    {
        var (factory, full) = await Mint(TokenKind.Service, []);
        await using (factory)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WaypointDbContext>();
            var resolver = new ServiceBearerResolver(db);
            var ctx = new DefaultHttpContext();
            ctx.Request.Headers.Authorization = $"Bearer {full}";
            ctx.Request.Headers["X-On-Behalf-Of-Label"] = "agent-x";
            var p = await resolver.ResolveAsync(ctx, CancellationToken.None);
            p!.PassthroughActorId.Should().BeNull();
            p.PassthroughActorLabel.Should().Be("agent-x");
        }
    }

    [Fact]
    public async Task Token_with_admin_kind_keeps_explicit_scopes_in_resolved_Principal()
    {
        var (factory, full) = await Mint(TokenKind.Admin, ["custom:read"]);
        await using (factory)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WaypointDbContext>();
            var resolver = new ServiceBearerResolver(db);
            var ctx = new DefaultHttpContext();
            ctx.Request.Headers.Authorization = $"Bearer {full}";
            var p = await resolver.ResolveAsync(ctx, CancellationToken.None);
            p!.Scopes.Should().Contain("admin");
            p.Scopes.Should().Contain("custom:read");
        }
    }
}
