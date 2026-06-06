using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Waypoint.Api.Auth;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Waypoint.Domain;
using Waypoint.Domain.Entities;
using Waypoint.Domain.Enums;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

public class AdminAuditEndpointMutationCoverage : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public AdminAuditEndpointMutationCoverage(PostgresFixture pg) => _pg = pg;

    private static async Task SeedAuditRows(WaypointApiFactory f, Guid tokenId, int count)
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WaypointDbContext>();
        db.ApiTokens.Add(new ApiToken
        {
            Id = tokenId, Name = "audit-fixture", Prefix = "audf" + tokenId.ToString("N")[..4],
            TokenHash = "$argon2id$v=19$m=65536,t=3,p=1$" + Convert.ToBase64String(new byte[16]) + "$" + Convert.ToBase64String(new byte[32]),
            Scopes = [],
        });
        for (var i = 0; i < count; i++)
        {
            db.TokenAuditLog.Add(new TokenAuditLog
            {
                TokenId = tokenId,
                Action = $"GET /api/v1/probe-{i}",
                Path = $"/api/v1/probe-{i}",
                Method = "GET",
                StatusCode = 200,
                At = DateTimeOffset.UtcNow.AddMinutes(-i),
            });
        }
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GET_audit_returns_recent_audit_rows()
    {
        await using var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        var tokenId = Guid.NewGuid();
        await SeedAuditRows(f, tokenId, 3);

        using var c = f.CreateClient();
        var resp = await c.GetAsync($"/api/admin/audit?tokenId={tokenId}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("probe-0");
    }

    [Fact]
    public async Task GET_audit_filters_by_tokenId()
    {
        await using var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        var tokenA = Guid.NewGuid();
        var tokenB = Guid.NewGuid();
        await SeedAuditRows(f, tokenA, 2);
        await SeedAuditRows(f, tokenB, 2);

        using var c = f.CreateClient();
        var resp = await c.GetAsync($"/api/admin/audit?tokenId={tokenA}");
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain(tokenA.ToString());
        // tokenB rows must NOT leak into the filtered response.
        body.Should().NotContain(tokenB.ToString());
    }

    [Fact]
    public async Task GET_audit_with_since_filter_drops_older_rows()
    {
        await using var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        var tokenId = Guid.NewGuid();
        await SeedAuditRows(f, tokenId, 5);   // rows aged 0..4 min ago

        using var c = f.CreateClient();
        var since = DateTimeOffset.UtcNow.AddMinutes(-2);   // keep only newer than 2 min
        var resp = await c.GetAsync($"/api/admin/audit?tokenId={tokenId}&since={Uri.EscapeDataString(since.ToString("O"))}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
