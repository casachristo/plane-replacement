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
/// Mutation-coverage tests for ServiceBearerResolver. Each test pins a specific
/// behavioral edge (bearer prefix check, parts count, revoked-token filter,
/// passthrough header handling) so the corresponding Stryker mutants get killed.
/// </summary>
public class ServiceBearerResolverTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public ServiceBearerResolverTests(PostgresFixture pg) => _pg = pg;

    private static async Task<(WaypointApiFactory factory, string fullToken)>
        Mint(PostgresFixture pg, TokenKind kind = TokenKind.Service, bool revoked = false)
    {
        var factory = new WaypointApiFactory { PostgresConnectionString = pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        var (prefix, full) = TokenHasher.GenerateNew();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WaypointDbContext>();
        db.ApiTokens.Add(new ApiToken
        {
            Name = "t-" + prefix,
            Prefix = prefix,
            TokenHash = TokenHasher.Hash(full),
            Scopes = [],
            Kind = kind,
            RevokedAt = revoked ? DateTimeOffset.UtcNow : null,
        });
        await db.SaveChangesAsync();
        return (factory, full);
    }

    private static async Task<Principal?> Resolve(WaypointApiFactory factory, string bearerHeaderValue,
        string? passthroughId = null, string? passthroughLabel = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WaypointDbContext>();
        var resolver = new ServiceBearerResolver(db);
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers.Authorization = bearerHeaderValue;
        if (passthroughId is not null) ctx.Request.Headers["X-On-Behalf-Of"] = passthroughId;
        if (passthroughLabel is not null) ctx.Request.Headers["X-On-Behalf-Of-Label"] = passthroughLabel;
        return await resolver.ResolveAsync(ctx, CancellationToken.None);
    }

    [Fact]
    public async Task No_Authorization_header_resolves_to_null()
    {
        var (factory, _) = await Mint(_pg);
        await using (factory)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WaypointDbContext>();
            var resolver = new ServiceBearerResolver(db);
            var ctx = new DefaultHttpContext();   // no Authorization header at all
            var p = await resolver.ResolveAsync(ctx, CancellationToken.None);
            p.Should().BeNull();
        }
    }

    [Fact]
    public async Task Authorization_not_starting_with_Bearer_wpt_resolves_to_null()
    {
        // Kills: "Bearer wpt_" → "" string mutation. If the prefix were "" any
        // header would match the StartsWith check and we'd fall through.
        var (factory, full) = await Mint(_pg);
        await using (factory)
        {
            // Right body, wrong prefix.
            var p = await Resolve(factory, "Bearer different_" + full[4..]);
            p.Should().BeNull();
        }
    }

    [Fact]
    public async Task Authorization_with_Basic_scheme_resolves_to_null()
    {
        var (factory, _) = await Mint(_pg);
        await using (factory)
        {
            var p = await Resolve(factory, "Basic dXNlcjpwYXNz");
            p.Should().BeNull();
        }
    }

    [Fact]
    public async Task Bearer_wpt_with_only_two_underscored_parts_resolves_to_null()
    {
        // Kills: parts.Length != 3 logical mutations.
        var (factory, _) = await Mint(_pg);
        await using (factory)
        {
            var p = await Resolve(factory, "Bearer wpt_onlytwo");
            p.Should().BeNull();
        }
    }

    [Fact]
    public async Task Bearer_wpt_with_wrong_prefix_length_resolves_to_null()
    {
        // Kills: parts[1].Length != 8 mutations.
        var (factory, _) = await Mint(_pg);
        await using (factory)
        {
            var p = await Resolve(factory, "Bearer wpt_short_somesecret");   // prefix=5 chars
            p.Should().BeNull();
        }
    }

    [Fact]
    public async Task Revoked_token_resolves_to_null()
    {
        // Kills: t.RevokedAt == null logical mutations in the Where clause.
        var (factory, full) = await Mint(_pg, revoked: true);
        await using (factory)
        {
            var p = await Resolve(factory, "Bearer " + full);
            p.Should().BeNull();
        }
    }

    [Fact]
    public async Task Token_with_matching_prefix_but_wrong_secret_resolves_to_null()
    {
        // Kills: hash verification logic; even when prefix matches, Verify must fail.
        var (factory, full) = await Mint(_pg);
        await using (factory)
        {
            var prefix = full.Split('_')[1];
            var tamperedSecret = "tampered-secret-zzzzzzzzzzzzz";
            var p = await Resolve(factory, $"Bearer wpt_{prefix}_{tamperedSecret}");
            p.Should().BeNull();
        }
    }

    [Fact]
    public async Task Passthrough_headers_populate_Principal_actor_fields()
    {
        // Kills: string.IsNullOrEmpty(passthroughId) → false/true conditional mutations.
        var (factory, full) = await Mint(_pg);
        await using (factory)
        {
            var p = await Resolve(factory, "Bearer " + full,
                passthroughId: "user-7", passthroughLabel: "Alice");
            p.Should().NotBeNull();
            p!.PassthroughActorId.Should().Be("user-7");
            p.PassthroughActorLabel.Should().Be("Alice");
        }
    }

    [Fact]
    public async Task Empty_passthrough_header_leaves_PassthroughActorId_null()
    {
        // Kills: the empty-string-check mutations and the (passthroughId != "") string mutation.
        var (factory, full) = await Mint(_pg);
        await using (factory)
        {
            // Header present but empty string → must not populate the actor id.
            var p = await Resolve(factory, "Bearer " + full,
                passthroughId: "", passthroughLabel: "");
            p.Should().NotBeNull();
            p!.PassthroughActorId.Should().BeNull();
            p.PassthroughActorLabel.Should().BeNull();
        }
    }
}
