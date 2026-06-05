using System.Net.Http.Json;
using FluentAssertions;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

public class IssueListPaginationMutationCoverage : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public IssueListPaginationMutationCoverage(PostgresFixture pg) => _pg = pg;

    private async Task<(HttpClient client, string slug)> SetupWith(int issueCount, string slug, string ident)
    {
        var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        var c = f.CreateClient();
        await c.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest(slug, "p", ident));
        for (var i = 0; i < issueCount; i++)
            await c.PostAsJsonAsync($"/api/v1/projects/{slug}/issues",
                new CreateIssueRequest($"issue {i}", "body"));
        return (c, slug);
    }

    [Fact]
    public async Task GET_list_default_limit_returns_up_to_50()
    {
        var (client, slug) = await SetupWith(3, "ipg1", "IPG1");
        var page = await (await client.GetAsync($"/api/v1/projects/{slug}/issues"))
            .Content.ReadFromJsonAsync<PagedResponse<IssueDto>>();
        page!.Data.Should().HaveCountLessThanOrEqualTo(50);
        page.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task GET_list_with_limit_query_param_clamps_at_that_size()
    {
        var (client, slug) = await SetupWith(7, "ipg2", "IPG2");
        var page = await (await client.GetAsync($"/api/v1/projects/{slug}/issues?limit=3"))
            .Content.ReadFromJsonAsync<PagedResponse<IssueDto>>();
        page!.Data.Should().HaveCount(3);
    }

    [Fact]
    public async Task GET_list_returns_TotalCount_independent_of_limit()
    {
        var (client, slug) = await SetupWith(5, "ipg3", "IPG3");
        var page = await (await client.GetAsync($"/api/v1/projects/{slug}/issues?limit=2"))
            .Content.ReadFromJsonAsync<PagedResponse<IssueDto>>();
        page!.TotalCount.Should().Be(5);
    }

    [Fact]
    public async Task GET_list_NextCursor_is_null_when_page_not_full()
    {
        var (client, slug) = await SetupWith(2, "ipg4", "IPG4");
        var page = await (await client.GetAsync($"/api/v1/projects/{slug}/issues?limit=50"))
            .Content.ReadFromJsonAsync<PagedResponse<IssueDto>>();
        page!.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task GET_list_NextCursor_is_set_when_page_is_full()
    {
        var (client, slug) = await SetupWith(5, "ipg5", "IPG5");
        var page = await (await client.GetAsync($"/api/v1/projects/{slug}/issues?limit=3"))
            .Content.ReadFromJsonAsync<PagedResponse<IssueDto>>();
        page!.NextCursor.Should().NotBeNullOrEmpty();
    }
}

public class AcceptanceCriterionPositionMutationCoverage : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public AcceptanceCriterionPositionMutationCoverage(PostgresFixture pg) => _pg = pg;

    private async Task<HttpClient> Setup(string slug, string ident)
    {
        var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        var c = f.CreateClient();
        await c.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest(slug, "p", ident));
        await c.PostAsJsonAsync($"/api/v1/projects/{slug}/issues",
            new CreateIssueRequest("t", "b"));
        return c;
    }

    [Fact]
    public async Task PATCH_AC_Position_updates_Position_field()
    {
        using var client = await Setup("acp1", "ACP1");
        var ac = await (await client.PostAsJsonAsync(
            "/api/v1/projects/acp1/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("at-1"))).Content.ReadFromJsonAsync<AcceptanceCriterionDto>();
        var resp = await client.PatchAsJsonAsync(
            $"/api/v1/projects/acp1/issues/1/acceptance-criteria/{ac!.Id}",
            new UpdateAcceptanceCriterionRequest(Position: 7));
        var updated = await resp.Content.ReadFromJsonAsync<AcceptanceCriterionDto>();
        updated!.Position.Should().Be(7);
    }

    [Fact]
    public async Task POST_AC_with_explicit_Position_uses_that_value()
    {
        using var client = await Setup("acp2", "ACP2");
        var resp = await client.PostAsJsonAsync(
            "/api/v1/projects/acp2/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("explicit", Position: 42));
        var dto = await resp.Content.ReadFromJsonAsync<AcceptanceCriterionDto>();
        dto!.Position.Should().Be(42);
    }

    [Fact]
    public async Task POST_AC_initial_Text_is_trimmed()
    {
        using var client = await Setup("acp3", "ACP3");
        var resp = await client.PostAsJsonAsync(
            "/api/v1/projects/acp3/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("   trimmed   "));
        var dto = await resp.Content.ReadFromJsonAsync<AcceptanceCriterionDto>();
        dto!.Text.Should().Be("trimmed");
    }

    [Fact]
    public async Task PATCH_AC_text_is_trimmed_on_update()
    {
        using var client = await Setup("acp4", "ACP4");
        var ac = await (await client.PostAsJsonAsync(
            "/api/v1/projects/acp4/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("ok"))).Content.ReadFromJsonAsync<AcceptanceCriterionDto>();
        var resp = await client.PatchAsJsonAsync(
            $"/api/v1/projects/acp4/issues/1/acceptance-criteria/{ac!.Id}",
            new UpdateAcceptanceCriterionRequest(Text: "  trimmed-text  "));
        var updated = await resp.Content.ReadFromJsonAsync<AcceptanceCriterionDto>();
        updated!.Text.Should().Be("trimmed-text");
    }

    [Fact]
    public async Task POST_AC_initial_state_is_unchecked()
    {
        using var client = await Setup("acp5", "ACP5");
        var resp = await client.PostAsJsonAsync(
            "/api/v1/projects/acp5/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("new"));
        var dto = await resp.Content.ReadFromJsonAsync<AcceptanceCriterionDto>();
        dto!.Checked.Should().BeFalse();
        dto.CheckedAt.Should().BeNull();
        dto.CheckedByActorType.Should().BeNull();
        dto.CheckedByActorId.Should().BeNull();
        dto.CheckedByActorLabel.Should().BeNull();
    }
}
