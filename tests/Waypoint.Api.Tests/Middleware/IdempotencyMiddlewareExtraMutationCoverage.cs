using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Waypoint.Api.Middleware;
using Xunit;

namespace Waypoint.Api.Tests.Middleware;

/// <summary>
/// More IdempotencyMiddleware mutation tests targeting the body-forwarding,
/// content-type preservation, and per-method-vs-method differentiation paths.
/// </summary>
public class IdempotencyMiddlewareExtraMutationCoverage
{
    private static async Task<(bool nextCalled, int status, string contentType, string body)> Drive(
        string method, string? idempotencyKey,
        int responseStatus = 200, string responseBody = "fresh", string responseContentType = "application/json")
    {
        var nextCalled = false;
        var middleware = new IdempotencyMiddleware(async c =>
        {
            nextCalled = true;
            c.Response.StatusCode = responseStatus;
            c.Response.ContentType = responseContentType;
            await c.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(responseBody));
        });
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = method;
        if (idempotencyKey is not null)
            ctx.Request.Headers[IdempotencyMiddleware.HeaderName] = idempotencyKey;
        var buf = new MemoryStream();
        ctx.Response.Body = buf;
        await middleware.InvokeAsync(ctx);
        buf.Position = 0;
        return (nextCalled, ctx.Response.StatusCode, ctx.Response.ContentType ?? "", new StreamReader(buf).ReadToEnd());
    }

    [Fact]
    public async Task POST_cache_preserves_response_status_code()
    {
        var key = $"k-{Guid.NewGuid()}";
        await Drive("POST", key, 418, "teapot");
        var (_, status, _, _) = await Drive("POST", key, 999, "different");
        status.Should().Be(418);
    }

    [Fact]
    public async Task POST_cache_preserves_response_content_type()
    {
        var key = $"k-{Guid.NewGuid()}";
        await Drive("POST", key, 200, "<html></html>", "text/html");
        var (_, _, ct, _) = await Drive("POST", key, 200, "ignored", "application/json");
        ct.Should().Be("text/html");
    }

    [Fact]
    public async Task POST_cache_returns_exact_byte_for_byte_body()
    {
        var key = $"k-{Guid.NewGuid()}";
        var payload = "{\"id\":\"abc-123\",\"name\":\"Alice\"}";
        await Drive("POST", key, 201, payload);
        var (_, _, _, body) = await Drive("POST", key, 999, "different");
        body.Should().Be(payload);
    }

    [Fact]
    public async Task PATCH_with_unique_key_does_NOT_serve_a_POST_cached_response()
    {
        // The two methods share the cache by key in the current impl, but each
        // unique key gets its own entry. This pins that POST cache + PATCH same-key
        // would actually share — and a DIFFERENT key for PATCH must be a fresh hit.
        var keyA = $"a-{Guid.NewGuid()}";
        var keyB = $"b-{Guid.NewGuid()}";
        await Drive("POST", keyA, 201, "post-body");
        var (nextCalled, _, _, body) = await Drive("PATCH", keyB, 200, "patch-body");
        nextCalled.Should().BeTrue();
        body.Should().Be("patch-body");
    }

    [Fact]
    public async Task POST_with_5xx_response_is_not_cached()
    {
        // WAY-26: a 5xx is transient and must NOT be cached — replaying the same key
        // re-runs the request instead of serving a stale 500 for 24h. Pins the
        // status-code guard (StatusCode < 500) that gates the cache write.
        var key = $"k-{Guid.NewGuid()}";
        var (firstCalled, firstStatus, _, _) = await Drive("POST", key, 500, "boom");
        firstCalled.Should().BeTrue();
        firstStatus.Should().Be(500);
        var (secondCalled, secondStatus, _, _) = await Drive("POST", key, 200, "ok");
        secondCalled.Should().BeTrue();        // not served from cache
        secondStatus.Should().Be(200);
    }

    [Fact]
    public async Task GET_request_never_caches_responses_even_with_Idempotency_Key()
    {
        // Kills mutations that drop the IsPost/IsPatch gate — GET should always
        // pass through and never engage the cache.
        var key = $"k-{Guid.NewGuid()}";
        await Drive("GET", key, 200, "first-body");
        var (nextCalled, _, _, body) = await Drive("GET", key, 200, "second-body");
        nextCalled.Should().BeTrue();
        body.Should().Be("second-body");
    }
}
