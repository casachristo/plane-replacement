using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Domain;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

/// <summary>
/// Mutation-coverage HTTP tests for AuthEndpoints. Focuses on /api/v1/whoami
/// (callable with the test fixture's injected Principal) and /auth/logout
/// (cookie-clearing path). The /auth/login challenge + /auth/post-login
/// callback paths require a real OIDC server stub — out of scope for this
/// batch, tracked in the mutation-debt ticket.
/// </summary>
public class AuthEndpointsMutationCoverage : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public AuthEndpointsMutationCoverage(PostgresFixture pg) => _pg = pg;

    private sealed record WhoAmI(string Kind, string Id, string DisplayName, string[] Scopes);

    [Fact]
    public async Task WhoAmI_with_test_principal_returns_kind_id_displayName_scopes()
    {
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();

        var resp = await client.GetAsync("/api/v1/whoami");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var me = await resp.Content.ReadFromJsonAsync<WhoAmI>();
        me!.Kind.Should().Be("Human");
        me.DisplayName.Should().Be("Test User");
        me.Scopes.Should().Contain("admin");
    }

    [Fact]
    public async Task WhoAmI_without_principal_returns_401()
    {
        await using var factory = new WaypointApiFactory
        {
            PostgresConnectionString = _pg.ConnectionString,
            TestPrincipal = null!,   // FixedPrincipalResolver returns null → no principal
        };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();

        var resp = await client.GetAsync("/api/v1/whoami");

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Auth_logout_returns_204()
    {
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();

        var resp = await client.PostAsync("/auth/logout", content: null);

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Auth_logout_clears_session_cookie_in_response()
    {
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", "waypoint_session=existing");

        var resp = await client.PostAsync("/auth/logout", content: null);

        resp.Headers.TryGetValues("Set-Cookie", out var setCookies).Should().BeTrue();
        string.Join("; ", setCookies!).Should().Contain("waypoint_session");
    }
}
