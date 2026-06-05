using System.Net;
using FluentAssertions;
using Waypoint.Api.Auth;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Domain;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

/// <summary>
/// Extra mutation-coverage for AuthEndpoints' reachable paths beyond whoami/logout:
/// /auth/login redirect-when-authenticated (the senior-review M2 fix from 89041dd)
/// and /auth/post-login 401 when no ASP.NET Core auth pipeline ran.
/// </summary>
public class AuthEndpointsExtraMutationCoverage : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public AuthEndpointsExtraMutationCoverage(PostgresFixture pg) => _pg = pg;

    [Fact]
    public async Task Auth_login_redirects_to_root_when_already_a_Human_principal()
    {
        // Default TestPrincipal is Human → short-circuit kicks in (the M2 fix).
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        var resp = await client.GetAsync("/auth/login");

        // 302/307 Redirect to "/"
        ((int)resp.StatusCode).Should().BeInRange(300, 399);
        resp.Headers.Location?.OriginalString.Should().Be("/");
    }

    [Fact]
    public async Task Auth_login_does_NOT_redirect_to_root_when_no_principal()
    {
        // No principal → the redirect short-circuit must not fire. The handler then
        // tries Results.Challenge which goes through ASP.NET Core auth pipeline →
        // OIDC challenge. In the Testing environment without OIDC wired, this still
        // results in a non-200, non-302-to-"/" outcome.
        await using var factory = new WaypointApiFactory
        {
            PostgresConnectionString = _pg.ConnectionString,
            TestPrincipal = null!,
        };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        var resp = await client.GetAsync("/auth/login");

        // Whatever it is, it must NOT be the "redirect to /" path.
        var redirectedToRoot = (int)resp.StatusCode is >= 300 and < 400
            && resp.Headers.Location?.OriginalString == "/";
        redirectedToRoot.Should().BeFalse();
    }

    [Fact]
    public async Task Auth_post_login_returns_401_when_aspnet_user_not_authenticated()
    {
        // /auth/post-login checks ctx.User.Identity?.IsAuthenticated (the ASP.NET Core
        // identity, NOT our Principal). In tests there's no OIDC pipeline so it's false,
        // and the endpoint returns 401.
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();

        var resp = await client.GetAsync("/auth/post-login");

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
