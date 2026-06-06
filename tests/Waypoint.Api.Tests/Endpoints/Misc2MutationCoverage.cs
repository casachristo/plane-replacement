using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Waypoint.Api.Endpoints.PublicApi;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

public class Misc2MutationCoverage : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public Misc2MutationCoverage(PostgresFixture pg) => _pg = pg;

    private WaypointApiFactory NewFactory() => new() { PostgresConnectionString = _pg.ConnectionString };

    [Fact]
    public async Task POST_token_DTO_Prefix_is_first_8_chars_of_full_token_payload()
    {
        await using var f = NewFactory();
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var resp = await c.PostAsJsonAsync("/api/admin/tokens",
            new CreateApiTokenRequest("prefix-test", Array.Empty<string>(), "Service"));
        var dto = await resp.Content.ReadFromJsonAsync<ApiTokenCreatedDto>();
        // Full token format: wpt_<8chars>_<rest>
        var fullParts = dto!.FullToken.Split('_');
        dto.Token.Prefix.Should().Be(fullParts[1]);
    }

    [Fact]
    public async Task GET_admin_audit_with_no_filters_returns_200_OK()
    {
        await using var f = NewFactory();
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var resp = await c.GetAsync("/api/admin/audit");
        ((int)resp.StatusCode).Should().Be(200);
    }

    [Fact]
    public async Task POST_token_with_Scopes_array_DTO_carries_same_values()
    {
        await using var f = NewFactory();
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var resp = await c.PostAsJsonAsync("/api/admin/tokens",
            new CreateApiTokenRequest("scoped", new[] { "issue:read", "issue:write", "comment:create" }, "Service"));
        var dto = await resp.Content.ReadFromJsonAsync<ApiTokenCreatedDto>();
        dto!.Token.Scopes.Should().BeEquivalentTo(new[] { "issue:read", "issue:write", "comment:create" });
    }

    [Fact]
    public async Task GET_admin_token_after_create_carries_RevokedAt_null()
    {
        await using var f = NewFactory();
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var created = await (await c.PostAsJsonAsync("/api/admin/tokens",
            new CreateApiTokenRequest("not-revoked", Array.Empty<string>(), "Service")))
            .Content.ReadFromJsonAsync<ApiTokenCreatedDto>();
        var list = await (await c.GetAsync("/api/admin/tokens"))
            .Content.ReadFromJsonAsync<List<ApiTokenDto>>();
        var dto = list!.First(t => t.Id == created!.Token.Id);
        dto.RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task GET_admin_token_after_create_carries_LastUsedAt_null()
    {
        await using var f = NewFactory();
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var created = await (await c.PostAsJsonAsync("/api/admin/tokens",
            new CreateApiTokenRequest("not-used", Array.Empty<string>(), "Service")))
            .Content.ReadFromJsonAsync<ApiTokenCreatedDto>();
        var list = await (await c.GetAsync("/api/admin/tokens"))
            .Content.ReadFromJsonAsync<List<ApiTokenDto>>();
        var dto = list!.First(t => t.Id == created!.Token.Id);
        dto.LastUsedAt.Should().BeNull();
    }
}
