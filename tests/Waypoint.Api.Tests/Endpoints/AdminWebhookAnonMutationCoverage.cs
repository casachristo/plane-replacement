using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Waypoint.Api.Endpoints.PublicApi;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

public class AdminWebhookAnonMutationCoverage : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public AdminWebhookAnonMutationCoverage(PostgresFixture pg) => _pg = pg;
    private WaypointApiFactory Anon() => new() { PostgresConnectionString = _pg.ConnectionString, TestPrincipal = null! };

    [Fact]
    public async Task GET_admin_tokens_anon_returns_401()
    {
        await using var f = Anon(); await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var resp = await c.GetAsync("/api/admin/tokens");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task POST_admin_tokens_anon_returns_401()
    {
        await using var f = Anon(); await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var resp = await c.PostAsJsonAsync("/api/admin/tokens",
            new CreateApiTokenRequest("x", Array.Empty<string>(), "Service"));
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DELETE_admin_tokens_anon_returns_401()
    {
        await using var f = Anon(); await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var resp = await c.DeleteAsync($"/api/admin/tokens/{Guid.NewGuid()}");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GET_admin_audit_anon_returns_401()
    {
        await using var f = Anon(); await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var resp = await c.GetAsync("/api/admin/audit");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GET_webhooks_anon_returns_401()
    {
        await using var f = Anon(); await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var resp = await c.GetAsync("/api/v1/webhooks");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task POST_webhooks_anon_returns_401()
    {
        await using var f = Anon(); await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var resp = await c.PostAsJsonAsync("/api/v1/webhooks",
            new CreateWebhookSubscriptionRequest("https://x.invalid/h", 1L, null));
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DELETE_webhook_anon_returns_401()
    {
        await using var f = Anon(); await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var resp = await c.DeleteAsync($"/api/v1/webhooks/{Guid.NewGuid()}");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GET_webhook_deliveries_anon_returns_401()
    {
        await using var f = Anon(); await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var resp = await c.GetAsync($"/api/v1/webhooks/{Guid.NewGuid()}/deliveries");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
