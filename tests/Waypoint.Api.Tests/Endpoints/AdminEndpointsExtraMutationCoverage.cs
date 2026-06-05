using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Waypoint.Api.Auth;
using Waypoint.Api.Endpoints.PublicApi;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Waypoint.Domain;
using Waypoint.Domain.Entities;
using Waypoint.Domain.Enums;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

public class AdminEndpointsExtraMutationCoverage : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public AdminEndpointsExtraMutationCoverage(PostgresFixture pg) => _pg = pg;

    private WaypointApiFactory NewFactory() => new() { PostgresConnectionString = _pg.ConnectionString };

    [Fact]
    public async Task POST_token_returned_DTO_has_correct_Name()
    {
        await using var factory = NewFactory();
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/admin/tokens",
            new CreateApiTokenRequest("named-token-xyz", new[] { "x" }, "Service"));
        var dto = await resp.Content.ReadFromJsonAsync<ApiTokenCreatedDto>();
        dto!.Token.Name.Should().Be("named-token-xyz");
    }

    [Fact]
    public async Task POST_token_returned_DTO_has_correct_Scopes()
    {
        await using var factory = NewFactory();
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/admin/tokens",
            new CreateApiTokenRequest("t", new[] { "read", "write", "transition" }, "Service"));
        var dto = await resp.Content.ReadFromJsonAsync<ApiTokenCreatedDto>();
        dto!.Token.Scopes.Should().BeEquivalentTo(new[] { "read", "write", "transition" });
    }

    [Fact]
    public async Task POST_token_returned_DTO_has_Service_kind_string()
    {
        await using var factory = NewFactory();
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/admin/tokens",
            new CreateApiTokenRequest("svc", Array.Empty<string>(), "Service"));
        var dto = await resp.Content.ReadFromJsonAsync<ApiTokenCreatedDto>();
        dto!.Token.Kind.Should().Be("Service");
    }

    [Fact]
    public async Task POST_token_FullToken_starts_with_wpt_prefix()
    {
        await using var factory = NewFactory();
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/admin/tokens",
            new CreateApiTokenRequest("t", Array.Empty<string>(), "Service"));
        var dto = await resp.Content.ReadFromJsonAsync<ApiTokenCreatedDto>();
        dto!.FullToken.Should().StartWith("wpt_");
    }

    [Fact]
    public async Task GET_tokens_returns_created_tokens_with_lookup_by_Id()
    {
        await using var factory = NewFactory();
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        var created = await (await client.PostAsJsonAsync("/api/admin/tokens",
            new CreateApiTokenRequest("look-me-up", Array.Empty<string>(), "Service")))
            .Content.ReadFromJsonAsync<ApiTokenCreatedDto>();

        var list = await (await client.GetAsync("/api/admin/tokens"))
            .Content.ReadFromJsonAsync<List<ApiTokenDto>>();
        list!.Any(t => t.Id == created!.Token.Id).Should().BeTrue();
    }

    [Fact]
    public async Task DELETE_token_already_deleted_returns_204_on_second_call_too()
    {
        // Idempotent-ish: deleting an already-revoked token still finds it (no soft
        // delete on api_tokens — just sets RevokedAt). Re-delete should succeed.
        await using var factory = NewFactory();
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        var created = await (await client.PostAsJsonAsync("/api/admin/tokens",
            new CreateApiTokenRequest("dup-del", Array.Empty<string>(), "Service")))
            .Content.ReadFromJsonAsync<ApiTokenCreatedDto>();
        var first = await client.DeleteAsync($"/api/admin/tokens/{created!.Token.Id}");
        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var second = await client.DeleteAsync($"/api/admin/tokens/{created.Token.Id}");
        second.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GET_audit_with_since_filter_does_not_500()
    {
        await using var factory = NewFactory();
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        var resp = await client.GetAsync("/api/admin/audit?since=2024-01-01T00:00:00Z");
        ((int)resp.StatusCode).Should().BeLessThan(500);
    }

    [Fact]
    public async Task GET_audit_with_token_id_filter_does_not_500()
    {
        await using var factory = NewFactory();
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        var resp = await client.GetAsync($"/api/admin/audit?tokenId={Guid.NewGuid()}");
        ((int)resp.StatusCode).Should().BeLessThan(500);
    }
}
