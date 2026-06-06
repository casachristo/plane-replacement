using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using System.Text;
using Waypoint.Api.Middleware;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

public class TargetedKills8MutationCoverage : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public TargetedKills8MutationCoverage(PostgresFixture pg) => _pg = pg;

    [Fact]
    public async Task Idempotency_cached_response_falls_back_to_application_json_when_no_content_type()
    {
        // The middleware uses `ctx.Response.ContentType ?? "application/json"` when caching.
        // Kills the null-coalescing-left mutation that would always return null.
        var key = $"ct-{Guid.NewGuid()}";
        var middleware = new IdempotencyMiddleware(async c =>
        {
            c.Response.StatusCode = 200;
            // Deliberately do NOT set ContentType.
            await c.Response.Body.WriteAsync(Encoding.UTF8.GetBytes("data"));
        });
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "POST";
        ctx.Request.Headers[IdempotencyMiddleware.HeaderName] = key;
        ctx.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(ctx);

        var middleware2 = new IdempotencyMiddleware(_ => Task.CompletedTask);
        var ctx2 = new DefaultHttpContext();
        ctx2.Request.Method = "POST";
        ctx2.Request.Headers[IdempotencyMiddleware.HeaderName] = key;
        ctx2.Response.Body = new MemoryStream();
        await middleware2.InvokeAsync(ctx2);
        ctx2.Response.ContentType.Should().Be("application/json");
    }

    [Fact]
    public async Task SurfaceGuard_path_internal_v1_with_BOTH_cookie_and_bearer_rejects_for_cookie()
    {
        // Both creds present on internal path: cookie causes reject (cookie is for public).
        var middleware = new Waypoint.Api.Auth.SurfaceGuardMiddleware(_ => Task.CompletedTask);
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/internal/v1/projects";
        ctx.Request.Headers.Cookie = Waypoint.Api.Auth.SurfaceGuardMiddleware.CookieName + "=abc";
        ctx.Request.Headers.Authorization = "Bearer wpt_test_secret";
        ctx.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(ctx);
        ctx.Response.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task RequestId_incoming_value_overrides_generated_GUID()
    {
        // Pins the contract that incoming header wins. Kills the "always-generate"
        // mutation that would discard the incoming value.
        var middleware = new RequestIdMiddleware(_ => Task.CompletedTask);
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers[RequestIdMiddleware.HeaderName] = "my-correlation-id-not-a-guid";
        await middleware.InvokeAsync(ctx);
        ctx.Items["RequestId"]?.ToString().Should().Be("my-correlation-id-not-a-guid");
    }
}
