using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Waypoint.Api.Auth;
using Waypoint.Api.Endpoints.PublicApi;
using Waypoint.Contracts;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Domain;
using Waypoint.Domain.Entities;
using Waypoint.Domain.Enums;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

/// <summary>
/// Mutation-coverage HTTP tests for AdminEndpoints. The default TestPrincipal has
/// the &quot;admin&quot; scope, so admin routes are reachable. A separate factory variant
/// with a non-admin Principal pins the negative path.
/// </summary>
public class AdminEndpointsMutationCoverage : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public AdminEndpointsMutationCoverage(PostgresFixture pg) => _pg = pg;

    private WaypointApiFactory NewFactory() => new() { PostgresConnectionString = _pg.ConnectionString };
    private WaypointApiFactory NewNonAdminFactory() => new()
    {
        PostgresConnectionString = _pg.ConnectionString,
        TestPrincipal = new Principal(
            PrincipalKind.Human, Guid.NewGuid().ToString(), "Non-Admin",
            ["issue:read"]),   // no "admin"
    };

    [Fact]
    public async Task GET_admin_tokens_returns_empty_list_initially()
    {
        await using var factory = NewFactory();
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        var resp = await client.GetAsync("/api/admin/tokens");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await resp.Content.ReadFromJsonAsync<List<ApiTokenDto>>();
        list.Should().NotBeNull();
    }

    [Fact]
    public async Task POST_admin_tokens_creates_a_token_with_201_and_returns_full_secret()
    {
        await using var factory = NewFactory();
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/admin/tokens",
            new CreateApiTokenRequest("agent-1", new[] { "issue:read" }, "Service"));
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await resp.Content.ReadFromJsonAsync<ApiTokenCreatedDto>();
        dto!.FullToken.Should().StartWith("wpt_");
        dto.Token.Name.Should().Be("agent-1");
        dto.Token.Prefix.Should().HaveLength(8);
    }

    [Fact]
    public async Task POST_admin_tokens_with_invalid_kind_returns_422()
    {
        await using var factory = NewFactory();
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/admin/tokens",
            new CreateApiTokenRequest("agent-2", new[] { "issue:read" }, "Bogus"));
        ((int)resp.StatusCode).Should().Be(422);
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Error.Code.Should().Be("invalid_kind");
    }

    [Fact]
    public async Task POST_admin_tokens_supports_Admin_kind()
    {
        await using var factory = NewFactory();
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/admin/tokens",
            new CreateApiTokenRequest("admin-token", Array.Empty<string>(), "Admin"));
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await resp.Content.ReadFromJsonAsync<ApiTokenCreatedDto>();
        dto!.Token.Kind.Should().Be("Admin");
    }

    [Fact]
    public async Task DELETE_admin_token_returns_204_and_marks_revoked()
    {
        await using var factory = NewFactory();
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        var created = await (await client.PostAsJsonAsync("/api/admin/tokens",
            new CreateApiTokenRequest("t-1", Array.Empty<string>(), "Service")))
            .Content.ReadFromJsonAsync<ApiTokenCreatedDto>();

        var del = await client.DeleteAsync($"/api/admin/tokens/{created!.Token.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WaypointDbContext>();
        var t = await db.ApiTokens.FindAsync(created.Token.Id);
        t!.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DELETE_unknown_admin_token_returns_404_token_not_found()
    {
        await using var factory = NewFactory();
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        var resp = await client.DeleteAsync($"/api/admin/tokens/{Guid.NewGuid()}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Error.Code.Should().Be("token_not_found");
    }

    [Fact]
    public async Task GET_admin_audit_returns_empty_baseline()
    {
        await using var factory = NewFactory();
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        var resp = await client.GetAsync("/api/admin/audit");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GET_admin_tokens_without_admin_scope_returns_422_missing_scope()
    {
        await using var factory = NewNonAdminFactory();
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        var resp = await client.GetAsync("/api/admin/tokens");
        ((int)resp.StatusCode).Should().Be(422);
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Error.Code.Should().Be("missing_scope");
    }

    [Fact]
    public async Task POST_admin_tokens_without_admin_scope_returns_422()
    {
        await using var factory = NewNonAdminFactory();
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/admin/tokens",
            new CreateApiTokenRequest("blocked", Array.Empty<string>(), "Service"));
        ((int)resp.StatusCode).Should().Be(422);
    }
}
