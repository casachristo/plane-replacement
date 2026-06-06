using System.Net.Http.Json;
using FluentAssertions;
using Waypoint.Api.Endpoints.PublicApi;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

public class AdminEndpointsExtra2MutationCoverage : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public AdminEndpointsExtra2MutationCoverage(PostgresFixture pg) => _pg = pg;

    private WaypointApiFactory NewFactory() => new() { PostgresConnectionString = _pg.ConnectionString };

    [Fact]
    public async Task POST_token_with_lowercase_Service_kind_works_ignoreCase()
    {
        await using var f = NewFactory();
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var resp = await c.PostAsJsonAsync("/api/admin/tokens",
            new CreateApiTokenRequest("lc", Array.Empty<string>(), "service"));   // lowercase
        var dto = await resp.Content.ReadFromJsonAsync<ApiTokenCreatedDto>();
        dto!.Token.Kind.Should().Be("Service");
    }

    [Fact]
    public async Task POST_token_with_lowercase_Admin_kind_works_ignoreCase()
    {
        await using var f = NewFactory();
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var resp = await c.PostAsJsonAsync("/api/admin/tokens",
            new CreateApiTokenRequest("lca", Array.Empty<string>(), "admin"));
        var dto = await resp.Content.ReadFromJsonAsync<ApiTokenCreatedDto>();
        dto!.Token.Kind.Should().Be("Admin");
    }

    [Fact]
    public async Task POST_token_returns_FullToken_with_three_parts_separated_by_underscore()
    {
        await using var f = NewFactory();
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var resp = await c.PostAsJsonAsync("/api/admin/tokens",
            new CreateApiTokenRequest("3p", Array.Empty<string>(), "Service"));
        var dto = await resp.Content.ReadFromJsonAsync<ApiTokenCreatedDto>();
        dto!.FullToken.Should().StartWith("wpt_");
        dto.FullToken[12].Should().Be('_');
        dto.FullToken.Split('_', 3).Length.Should().Be(3);
    }

    [Fact]
    public async Task GET_tokens_returns_DTO_with_Kind_string_for_Admin_tokens()
    {
        await using var f = NewFactory();
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        await c.PostAsJsonAsync("/api/admin/tokens",
            new CreateApiTokenRequest("akt", Array.Empty<string>(), "Admin"));
        var list = await (await c.GetAsync("/api/admin/tokens"))
            .Content.ReadFromJsonAsync<List<ApiTokenDto>>();
        list!.Any(t => t.Kind == "Admin").Should().BeTrue();
    }

    [Fact]
    public async Task DELETE_token_sets_RevokedAt_visible_in_subsequent_GET()
    {
        await using var f = NewFactory();
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var created = await (await c.PostAsJsonAsync("/api/admin/tokens",
            new CreateApiTokenRequest("rvk", Array.Empty<string>(), "Service")))
            .Content.ReadFromJsonAsync<ApiTokenCreatedDto>();
        await c.DeleteAsync($"/api/admin/tokens/{created!.Token.Id}");
        var list = await (await c.GetAsync("/api/admin/tokens"))
            .Content.ReadFromJsonAsync<List<ApiTokenDto>>();
        var dto = list!.First(t => t.Id == created.Token.Id);
        dto.RevokedAt.Should().NotBeNull();
    }
}
