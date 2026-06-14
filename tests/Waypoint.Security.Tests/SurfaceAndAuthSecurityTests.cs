using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Waypoint.Api.Auth;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Xunit;

namespace Waypoint.Security.Tests;

// WAY-31: security tier. Covers the surface guard (public vs internal credential
// confusion), proxy-header identity spoofing on the internal surface, and
// scope-bypass on write endpoints. Asserts the exact 401/403 codes.
public class SurfaceAndAuthSecurityTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public SurfaceAndAuthSecurityTests(PostgresFixture pg) => _pg = pg;

    private WaypointApiFactory Factory(params string[] scopes) => new()
    {
        PostgresConnectionString = _pg.ConnectionString,
        TestPrincipal = new Principal(PrincipalKind.InternalService, System.Guid.NewGuid().ToString(), "svc",
            scopes.Length == 0
                ? new[] { "issue:read", "issue:create", "issue:transition", "comment:create", "admin" }
                : scopes),
    };

    [Fact]
    public async Task Public_surface_rejects_a_service_bearer_token_with_401()
    {
        await using var f = Factory();
        await f.EnsureMigratedAsync();
        using var client = f.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/projects/x/issues");
        req.Headers.TryAddWithoutValidation("Authorization", "Bearer wpt_abcd1234_secret");
        var resp = await client.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await resp.Content.ReadAsStringAsync()).Should().Contain("not_for_public_api");
    }

    [Fact]
    public async Task Internal_surface_rejects_a_browser_session_cookie_with_401()
    {
        await using var f = Factory();
        await f.EnsureMigratedAsync();
        using var client = f.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Get, "/internal/v1/projects/x/issues");
        req.Headers.TryAddWithoutValidation("Cookie", "waypoint_session=deadbeef");
        var resp = await client.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await resp.Content.ReadAsStringAsync()).Should().Contain("not_for_internal_api");
    }

    [Fact]
    public async Task Read_only_token_is_forbidden_from_writing_403()
    {
        await using var admin = Factory("admin");
        await admin.EnsureMigratedAsync();
        using var ac = admin.CreateClient();
        await ac.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("sec-w", "S", "SEW"));

        await using var ro = Factory("issue:read");
        using var roc = ro.CreateClient();
        var resp = await roc.PostAsJsonAsync("/api/v1/projects/sec-w/issues", new CreateIssueRequest("nope", "x"));
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Spoofed_proxy_identity_header_does_not_authenticate_when_untrusted()
    {
        // AutheliaHeaderResolver is inert unless TrustProxyHeaders is explicitly enabled, so a
        // spoofed Remote-Email on the internal surface cannot impersonate a human by default.
        var resolver = new AutheliaHeaderResolver(Options.Create(new OidcOptions { TrustProxyHeaders = false }));
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["Remote-Email"] = "attacker@evil.example";
        var p = await resolver.ResolveAsync(ctx, System.Threading.CancellationToken.None);
        p.Should().BeNull();
    }

    [Fact]
    public async Task Proxy_identity_header_authenticates_only_when_trusted()
    {
        var resolver = new AutheliaHeaderResolver(Options.Create(new OidcOptions { TrustProxyHeaders = true }));
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["Remote-Email"] = "user@chris.box";
        var p = await resolver.ResolveAsync(ctx, System.Threading.CancellationToken.None);
        p.Should().NotBeNull();
        p!.Kind.Should().Be(PrincipalKind.Human);
    }
}
