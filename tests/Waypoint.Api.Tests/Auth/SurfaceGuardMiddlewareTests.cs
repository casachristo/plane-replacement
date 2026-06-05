using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Waypoint.Api.Auth;
using Xunit;

namespace Waypoint.Api.Tests.Auth;

/// <summary>
/// Mutation-coverage tests for SurfaceGuardMiddleware. Pin every branch of the
/// four-case truth table (path prefix x credential type) plus the "no match,
/// pass through" path.
/// </summary>
public class SurfaceGuardMiddlewareTests
{
    private static async Task<(int status, bool nextCalled)> Invoke(string path,
        string? authorization = null, bool waypointSessionCookie = false)
    {
        var nextCalled = false;
        var middleware = new SurfaceGuardMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = path;
        if (authorization is not null) ctx.Request.Headers.Authorization = authorization;
        if (waypointSessionCookie)
        {
            ctx.Request.Headers.Cookie = SurfaceGuardMiddleware.CookieName + "=abc";
        }
        ctx.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(ctx);
        return (ctx.Response.StatusCode, nextCalled);
    }

    [Fact]
    public async Task Public_api_with_Bearer_wpt_token_returns_401_and_does_not_pass()
    {
        var (status, nextCalled) = await Invoke("/api/v1/projects", authorization: "Bearer wpt_test_secret");
        status.Should().Be(401);
        nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Public_api_with_no_auth_passes_through()
    {
        var (_, nextCalled) = await Invoke("/api/v1/projects");
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Public_api_with_Bearer_NOT_wpt_passes_through()
    {
        // Kills: "Bearer wpt_" → "" string mutation. With prefix "", every Bearer
        // header would trip the reject.
        var (_, nextCalled) = await Invoke("/api/v1/projects", authorization: "Bearer github_pat_xyz");
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Public_api_with_session_cookie_passes_through()
    {
        // Cookie is for the public surface — cookie + public must pass through.
        var (_, nextCalled) = await Invoke("/api/v1/projects", waypointSessionCookie: true);
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Internal_api_with_session_cookie_returns_401_and_does_not_pass()
    {
        var (status, nextCalled) = await Invoke("/internal/v1/projects", waypointSessionCookie: true);
        status.Should().Be(401);
        nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Internal_api_with_no_cookie_passes_through()
    {
        var (_, nextCalled) = await Invoke("/internal/v1/projects");
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Internal_api_with_Bearer_wpt_token_passes_through()
    {
        // Bearer is for the internal surface — bearer + internal must pass through.
        var (_, nextCalled) = await Invoke("/internal/v1/projects", authorization: "Bearer wpt_test_secret");
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Healthz_path_with_anything_passes_through()
    {
        // Kills: "/api/v1/" → "" path-prefix mutation. With prefix "", every path
        // would match the public-api check.
        var (_, nextCalled) = await Invoke("/healthz/live", authorization: "Bearer wpt_test_secret");
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Auth_login_path_passes_through_with_no_creds()
    {
        // /auth/* doesn't match either prefix; baseline pass-through.
        var (_, nextCalled) = await Invoke("/auth/login");
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Public_api_401_response_includes_not_for_public_api_code()
    {
        var middleware = new SurfaceGuardMiddleware(_ => Task.CompletedTask);
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/api/v1/projects";
        ctx.Request.Headers.Authorization = "Bearer wpt_test_secret";
        var body = new MemoryStream();
        ctx.Response.Body = body;
        await middleware.InvokeAsync(ctx);
        body.Position = 0;
        var json = new StreamReader(body).ReadToEnd();
        json.Should().Contain("not_for_public_api");
    }

    [Fact]
    public async Task Internal_api_401_response_includes_not_for_internal_api_code()
    {
        var middleware = new SurfaceGuardMiddleware(_ => Task.CompletedTask);
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/internal/v1/projects";
        ctx.Request.Headers.Cookie = SurfaceGuardMiddleware.CookieName + "=abc";
        var body = new MemoryStream();
        ctx.Response.Body = body;
        await middleware.InvokeAsync(ctx);
        body.Position = 0;
        var json = new StreamReader(body).ReadToEnd();
        json.Should().Contain("not_for_internal_api");
    }
}
