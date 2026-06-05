using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Waypoint.Api.Auth;
using Waypoint.Contracts;
using Xunit;

namespace Waypoint.Api.Tests.Auth;

public class SurfaceGuardMiddlewareExtraMutationCoverage
{
    private static async Task<DefaultHttpContext> Drive(string path,
        string? authorization = null, bool waypointSessionCookie = false)
    {
        var middleware = new SurfaceGuardMiddleware(_ => Task.CompletedTask);
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = path;
        if (authorization is not null) ctx.Request.Headers.Authorization = authorization;
        if (waypointSessionCookie)
            ctx.Request.Headers.Cookie = SurfaceGuardMiddleware.CookieName + "=abc";
        ctx.Items["RequestId"] = "test-req-id";
        ctx.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(ctx);
        return ctx;
    }

    [Fact]
    public async Task Public_api_reject_response_uses_application_json_content_type()
    {
        var ctx = await Drive("/api/v1/projects", authorization: "Bearer wpt_test_secret");
        ctx.Response.ContentType.Should().Be("application/json");
    }

    [Fact]
    public async Task Public_api_reject_response_carries_RequestId_in_envelope()
    {
        var ctx = await Drive("/api/v1/projects", authorization: "Bearer wpt_test_secret");
        ctx.Response.Body.Position = 0;
        var envelope = await JsonSerializer.DeserializeAsync<ErrorResponse>(ctx.Response.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        envelope!.RequestId.Should().Be("test-req-id");
    }

    [Fact]
    public async Task Internal_api_reject_response_carries_a_RequestId_even_when_not_set_in_Items()
    {
        var middleware = new SurfaceGuardMiddleware(_ => Task.CompletedTask);
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/internal/v1/projects";
        ctx.Request.Headers.Cookie = SurfaceGuardMiddleware.CookieName + "=abc";
        // No RequestId in Items — middleware must still produce a valid one.
        ctx.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(ctx);
        ctx.Response.Body.Position = 0;
        var envelope = await JsonSerializer.DeserializeAsync<ErrorResponse>(ctx.Response.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        envelope!.RequestId.Should().NotBeNullOrEmpty();
        Guid.TryParse(envelope.RequestId, out _).Should().BeTrue();
    }

    [Fact]
    public async Task Public_api_reject_body_contains_explanatory_message_about_internal_surface()
    {
        var ctx = await Drive("/api/v1/projects", authorization: "Bearer wpt_test_secret");
        ctx.Response.Body.Position = 0;
        var text = new StreamReader(ctx.Response.Body).ReadToEnd();
        text.Should().Contain("internal");
    }

    [Fact]
    public async Task Internal_api_reject_body_contains_explanatory_message_about_public_surface()
    {
        var ctx = await Drive("/internal/v1/projects", waypointSessionCookie: true);
        ctx.Response.Body.Position = 0;
        var text = new StreamReader(ctx.Response.Body).ReadToEnd();
        text.Should().Contain("public");
    }

    [Fact]
    public async Task Path_that_is_just_root_passes_through()
    {
        var middleware = new SurfaceGuardMiddleware(_ => Task.CompletedTask);
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/";
        ctx.Request.Headers.Authorization = "Bearer wpt_test_secret";
        var nextWasCalled = false;
        var mw2 = new SurfaceGuardMiddleware(_ => { nextWasCalled = true; return Task.CompletedTask; });
        await mw2.InvokeAsync(ctx);
        nextWasCalled.Should().BeTrue();
    }
}
