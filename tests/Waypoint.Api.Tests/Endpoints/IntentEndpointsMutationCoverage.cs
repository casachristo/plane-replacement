using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Waypoint.Api.Auth;
using Waypoint.Api.Endpoints.InternalApi;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Waypoint.Domain;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

/// <summary>
/// Mutation-coverage HTTP tests for the internal-surface IntentEndpoints.
/// Drives /internal/v1/* with an InternalService TestPrincipal so SurfaceGuard
/// + the Kind != Human guard both pass.
/// </summary>
public class IntentEndpointsMutationCoverage : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public IntentEndpointsMutationCoverage(PostgresFixture pg) => _pg = pg;

    private static readonly Guid TestTokenId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private WaypointApiFactory NewServiceFactory() => new()
    {
        PostgresConnectionString = _pg.ConnectionString,
        TestPrincipal = new Principal(
            PrincipalKind.InternalService, TestTokenId.ToString(),
            "test-service", ["intent:file"]),
    };

    private WaypointApiFactory NewHumanFactory() => new()
    {
        PostgresConnectionString = _pg.ConnectionString,
        // Default Human principal (admin scope etc.).
    };

    private static async Task EnsureProject(HttpClient client, string slug, string ident)
    {
        // POST against the internal surface so the Bearer-rejection rules don't trip.
        await client.PostAsJsonAsync("/internal/v1/projects",
            new CreateProjectRequest(slug, "Test", ident));
    }

    [Fact]
    public async Task POST_intent_as_Human_returns_422_internal_service_required()
    {
        await using var factory = NewHumanFactory();
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        // Even creating the project via public is fine, this test never reaches that.
        var resp = await client.PostAsJsonAsync(
            "/internal/v1/projects/any/intents",
            new FileIntentRequest("/src/Foo", "implement Foo"));
        ((int)resp.StatusCode).Should().Be(422);
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Error.Code.Should().Be("internal_service_required");
    }

    [Fact]
    public async Task DELETE_intent_as_Human_returns_422_internal_service_required()
    {
        await using var factory = NewHumanFactory();
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        var resp = await client.DeleteAsync($"/internal/v1/intents/{Guid.NewGuid()}");
        ((int)resp.StatusCode).Should().Be(422);
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Error.Code.Should().Be("internal_service_required");
    }

    [Fact]
    public async Task POST_intent_against_unknown_project_returns_404_project_not_found()
    {
        await using var factory = NewServiceFactory();
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync(
            "/internal/v1/projects/does-not-exist/intents",
            new FileIntentRequest("/src/Foo", "implement Foo"));
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Error.Code.Should().Be("project_not_found");
    }

    [Fact]
    public async Task POST_intent_with_non_Guid_principal_id_returns_422_invalid_principal()
    {
        await using var factory = new WaypointApiFactory
        {
            PostgresConnectionString = _pg.ConnectionString,
            TestPrincipal = new Principal(
                PrincipalKind.InternalService, "not-a-guid", "bad-token", []),
        };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        await EnsureProject(client, "intp1", "INTP1");
        var resp = await client.PostAsJsonAsync(
            "/internal/v1/projects/intp1/intents",
            new FileIntentRequest("/src/Foo", "implement Foo"));
        ((int)resp.StatusCode).Should().Be(422);
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Error.Code.Should().Be("invalid_principal");
    }

    [Fact]
    public async Task DELETE_unknown_intent_with_valid_service_principal_returns_404()
    {
        await using var factory = NewServiceFactory();
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        // Repo treats missing intent as 404.
        var resp = await client.DeleteAsync($"/internal/v1/intents/{Guid.NewGuid()}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
