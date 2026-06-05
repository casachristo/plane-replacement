using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Waypoint.Api.Middleware;
using Xunit;

namespace Waypoint.Api.Tests.Middleware;

/// <summary>
/// Mutation-coverage tests for RequestIdMiddleware. The middleware reads incoming
/// X-Request-Id when present, generates a new GUID otherwise, stashes it in
/// HttpContext.Items, and echoes it on the response.
/// </summary>
public class RequestIdMiddlewareMutationCoverage
{
    private static async Task<HttpContext> Invoke(string? incoming = null)
    {
        var middleware = new RequestIdMiddleware(_ => Task.CompletedTask);
        var ctx = new DefaultHttpContext();
        if (incoming is not null) ctx.Request.Headers[RequestIdMiddleware.HeaderName] = incoming;
        await middleware.InvokeAsync(ctx);
        return ctx;
    }

    [Fact]
    public async Task Incoming_X_Request_Id_is_echoed_to_Items()
    {
        var ctx = await Invoke("client-correlation-42");
        ctx.Items["RequestId"].Should().Be("client-correlation-42");
    }

    [Fact]
    public async Task Missing_X_Request_Id_generates_a_GUID_into_Items()
    {
        var ctx = await Invoke();
        var rid = ctx.Items["RequestId"]?.ToString();
        rid.Should().NotBeNullOrEmpty();
        Guid.TryParse(rid, out _).Should().BeTrue();
    }

    [Fact]
    public async Task Empty_X_Request_Id_is_treated_as_missing_and_a_GUID_is_generated()
    {
        // Kills: !string.IsNullOrWhiteSpace mutations. An empty incoming header
        // should NOT be honored as the request id.
        var ctx = await Invoke("   ");
        var rid = ctx.Items["RequestId"]?.ToString();
        Guid.TryParse(rid, out _).Should().BeTrue();   // a GUID, not whitespace
    }

    [Fact]
    public async Task RequestId_is_set_BEFORE_next_runs()
    {
        // RequestId must be present in Items by the time downstream middleware runs.
        string? observed = null;
        var middleware = new RequestIdMiddleware(c => { observed = c.Items["RequestId"]?.ToString(); return Task.CompletedTask; });
        var ctx = new DefaultHttpContext();
        await middleware.InvokeAsync(ctx);
        observed.Should().NotBeNull();
    }

    [Fact]
    public void HeaderName_constant_is_X_Request_Id()
    {
        RequestIdMiddleware.HeaderName.Should().Be("X-Request-Id");
    }
}
