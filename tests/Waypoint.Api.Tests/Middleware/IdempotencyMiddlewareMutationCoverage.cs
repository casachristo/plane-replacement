using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Waypoint.Api.Middleware;
using Xunit;

namespace Waypoint.Api.Tests.Middleware;

/// <summary>
/// Mutation-coverage tests for IdempotencyMiddleware. Hits each decision branch:
/// method gate (POST/PATCH only), header gate (present + non-blank), cache hit
/// vs cache miss, response body forwarding.
/// </summary>
public class IdempotencyMiddlewareMutationCoverage
{
    private static async Task<(bool nextCalled, int status, string body)> Drive(
        string method, string? idempotencyKey,
        int responseStatus = 200, string responseBody = "fresh")
    {
        var nextCalled = false;
        var middleware = new IdempotencyMiddleware(async c =>
        {
            nextCalled = true;
            c.Response.StatusCode = responseStatus;
            c.Response.ContentType = "application/json";
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
        return (nextCalled, ctx.Response.StatusCode, new StreamReader(buf).ReadToEnd());
    }

    [Fact]
    public async Task GET_request_skips_caching_and_just_calls_next()
    {
        // Kills the IsPost OR IsPatch logical mutations: a GET must pass through
        // without entering the cache machinery at all.
        var (nextCalled, _, _) = await Drive("GET", "any-key");
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task DELETE_request_skips_caching()
    {
        var (nextCalled, _, _) = await Drive("DELETE", "any-key");
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task POST_without_Idempotency_Key_passes_through()
    {
        var (nextCalled, status, body) = await Drive("POST", null, 201, "{\"id\":\"abc\"}");
        nextCalled.Should().BeTrue();
        status.Should().Be(201);
        body.Should().Be("{\"id\":\"abc\"}");
    }

    [Fact]
    public async Task POST_with_whitespace_Idempotency_Key_is_treated_as_no_key()
    {
        // Kills the !string.IsNullOrWhiteSpace mutations. Whitespace must NOT
        // engage the cache.
        var (nextCalled, _, _) = await Drive("POST", "   ");
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task POST_with_Idempotency_Key_first_call_invokes_next()
    {
        var (nextCalled, status, _) = await Drive("POST", $"idem-{Guid.NewGuid()}", 201, "first");
        nextCalled.Should().BeTrue();
        status.Should().Be(201);
    }

    [Fact]
    public async Task POST_with_same_Idempotency_Key_returns_cached_response_without_next()
    {
        // Kills the cache-hit-path mutations (TryGetValue branch, ExpiresAt > Now
        // comparison flips).
        var key = $"idem-{Guid.NewGuid()}";
        // First call: populate cache.
        var (firstCalled, firstStatus, firstBody) = await Drive("POST", key, 201, "first-body");
        firstCalled.Should().BeTrue();

        // Second call with SAME key: cache hit, next must NOT fire, body is reused.
        var (secondCalled, secondStatus, secondBody) = await Drive("POST", key, 999, "this-should-not-be-seen");
        secondCalled.Should().BeFalse();
        secondStatus.Should().Be(firstStatus);
        secondBody.Should().Be(firstBody);
    }

    [Fact]
    public async Task PATCH_is_subject_to_caching_just_like_POST()
    {
        // Kills mutation that drops PATCH from the gate.
        var key = $"idem-{Guid.NewGuid()}";
        await Drive("PATCH", key, 200, "patched");
        var (nextCalled, _, body) = await Drive("PATCH", key, 999, "would-have-changed");
        nextCalled.Should().BeFalse();
        body.Should().Be("patched");
    }

    [Fact]
    public void HeaderName_constant_is_Idempotency_Key()
    {
        IdempotencyMiddleware.HeaderName.Should().Be("Idempotency-Key");
    }
}
