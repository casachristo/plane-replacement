using System.Net.Http.Json;
using FluentAssertions;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

public class TargetedKills7MutationCoverage : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public TargetedKills7MutationCoverage(PostgresFixture pg) => _pg = pg;

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
    public async Task AC_check_returns_DTO_with_Checked_true_and_recent_CheckedAt()
    {
        using var c = await Setup("tk7a", "TK7A");
        var ac = await (await c.PostAsJsonAsync(
            "/api/v1/projects/tk7a/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("test"))).Content.ReadFromJsonAsync<AcceptanceCriterionDto>();
        var dto = await (await c.PostAsync(
            $"/api/v1/projects/tk7a/issues/1/acceptance-criteria/{ac!.Id}/check", content: null))
            .Content.ReadFromJsonAsync<AcceptanceCriterionDto>();
        dto!.Checked.Should().BeTrue();
        dto.CheckedAt.Should().NotBeNull();
        dto.CheckedAt.Value.Should().BeAfter(DateTimeOffset.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task AC_check_records_CheckedByActorType_User_for_default_test_principal()
    {
        using var c = await Setup("tk7b", "TK7B");
        var ac = await (await c.PostAsJsonAsync(
            "/api/v1/projects/tk7b/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("test"))).Content.ReadFromJsonAsync<AcceptanceCriterionDto>();
        var dto = await (await c.PostAsync(
            $"/api/v1/projects/tk7b/issues/1/acceptance-criteria/{ac!.Id}/check", content: null))
            .Content.ReadFromJsonAsync<AcceptanceCriterionDto>();
        dto!.CheckedByActorType.Should().Be("User");
    }

    [Fact]
    public async Task AC_GET_list_returns_all_created_in_position_order()
    {
        using var c = await Setup("tk7c", "TK7C");
        await c.PostAsJsonAsync("/api/v1/projects/tk7c/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("x", Position: 2));
        await c.PostAsJsonAsync("/api/v1/projects/tk7c/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("y", Position: 1));

        var list = await (await c.GetAsync("/api/v1/projects/tk7c/issues/1/acceptance-criteria"))
            .Content.ReadFromJsonAsync<List<AcceptanceCriterionDto>>();
        list.Should().HaveCount(2);
        list!.Should().BeInAscendingOrder(a => a.Position);
    }
}
