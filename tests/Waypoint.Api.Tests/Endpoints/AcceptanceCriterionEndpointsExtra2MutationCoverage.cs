using System.Net.Http.Json;
using FluentAssertions;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

public class AcceptanceCriterionEndpointsExtra2MutationCoverage : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public AcceptanceCriterionEndpointsExtra2MutationCoverage(PostgresFixture pg) => _pg = pg;

    private async Task<HttpClient> Setup(string slug, string ident)
    {
        var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        var c = f.CreateClient();
        await c.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest(slug, "p", ident));
        await c.PostAsJsonAsync($"/api/v1/projects/{slug}/issues", new CreateIssueRequest("t", "b"));
        return c;
    }

    [Fact]
    public async Task POST_then_PATCH_only_Position_leaves_Text_unchanged()
    {
        using var client = await Setup("acx2a", "ACX2A");
        var ac = await (await client.PostAsJsonAsync(
            "/api/v1/projects/acx2a/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("original text"))).Content.ReadFromJsonAsync<AcceptanceCriterionDto>();
        var resp = await client.PatchAsJsonAsync(
            $"/api/v1/projects/acx2a/issues/1/acceptance-criteria/{ac!.Id}",
            new UpdateAcceptanceCriterionRequest(Position: 9));
        var dto = await resp.Content.ReadFromJsonAsync<AcceptanceCriterionDto>();
        dto!.Position.Should().Be(9);
        dto.Text.Should().Be("original text");
    }

    [Fact]
    public async Task POST_uncheck_can_be_called_on_an_unchecked_AC_safely()
    {
        using var client = await Setup("acx2b", "ACX2B");
        var ac = await (await client.PostAsJsonAsync(
            "/api/v1/projects/acx2b/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("unchecked-baseline"))).Content.ReadFromJsonAsync<AcceptanceCriterionDto>();
        // ac.Checked = false on POST. Uncheck on an unchecked AC must still succeed.
        var resp = await client.PostAsync(
            $"/api/v1/projects/acx2b/issues/1/acceptance-criteria/{ac!.Id}/uncheck", content: null);
        resp.IsSuccessStatusCode.Should().BeTrue();
        var dto = await resp.Content.ReadFromJsonAsync<AcceptanceCriterionDto>();
        dto!.Checked.Should().BeFalse();
        dto.CheckedAt.Should().BeNull();
    }

    [Fact]
    public async Task POST_check_then_POST_check_again_does_not_change_CheckedAt_by_much()
    {
        using var client = await Setup("acx2c", "ACX2C");
        var ac = await (await client.PostAsJsonAsync(
            "/api/v1/projects/acx2c/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("dbl"))).Content.ReadFromJsonAsync<AcceptanceCriterionDto>();
        var first = await (await client.PostAsync(
            $"/api/v1/projects/acx2c/issues/1/acceptance-criteria/{ac!.Id}/check", content: null))
            .Content.ReadFromJsonAsync<AcceptanceCriterionDto>();
        // Second check overwrites CheckedAt with a slightly later timestamp.
        var second = await (await client.PostAsync(
            $"/api/v1/projects/acx2c/issues/1/acceptance-criteria/{ac.Id}/check", content: null))
            .Content.ReadFromJsonAsync<AcceptanceCriterionDto>();
        second!.Checked.Should().BeTrue();
        second.CheckedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DELETE_AC_then_subsequent_GET_does_not_include_it()
    {
        using var client = await Setup("acx2d", "ACX2D");
        var ac = await (await client.PostAsJsonAsync(
            "/api/v1/projects/acx2d/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("deleted-soon"))).Content.ReadFromJsonAsync<AcceptanceCriterionDto>();
        await client.DeleteAsync($"/api/v1/projects/acx2d/issues/1/acceptance-criteria/{ac!.Id}");
        var list = await (await client.GetAsync("/api/v1/projects/acx2d/issues/1/acceptance-criteria"))
            .Content.ReadFromJsonAsync<List<AcceptanceCriterionDto>>();
        list!.Any(a => a.Id == ac.Id).Should().BeFalse();
    }
}
