using System.Net.Http.Json;
using FluentAssertions;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

public class TargetedKills4MutationCoverage : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public TargetedKills4MutationCoverage(PostgresFixture pg) => _pg = pg;

    [Fact]
    public async Task Issue_GET_with_AC_returns_AcceptanceCriteria_array_with_correct_count()
    {
        await using var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        await c.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("tk4a", "p", "TK4A"));
        await c.PostAsJsonAsync("/api/v1/projects/tk4a/issues", new CreateIssueRequest("t", "b"));
        await c.PostAsJsonAsync("/api/v1/projects/tk4a/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("one"));
        await c.PostAsJsonAsync("/api/v1/projects/tk4a/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("two"));

        var issue = await (await c.GetAsync("/api/v1/projects/tk4a/issues/1"))
            .Content.ReadFromJsonAsync<IssueDto>();
        issue!.AcceptanceCriteria.Should().HaveCount(2);
    }

    [Fact]
    public async Task Issue_GET_with_no_AC_returns_AcceptanceCriteria_as_empty_array_not_null()
    {
        await using var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        await c.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("tk4b", "p", "TK4B"));
        await c.PostAsJsonAsync("/api/v1/projects/tk4b/issues", new CreateIssueRequest("t", "b"));

        var issue = await (await c.GetAsync("/api/v1/projects/tk4b/issues/1"))
            .Content.ReadFromJsonAsync<IssueDto>();
        issue!.AcceptanceCriteria.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task GET_list_returns_data_for_each_created_issue_with_correct_titles()
    {
        await using var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        await c.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("tk4c", "p", "TK4C"));
        await c.PostAsJsonAsync("/api/v1/projects/tk4c/issues", new CreateIssueRequest("title-A", "x"));
        await c.PostAsJsonAsync("/api/v1/projects/tk4c/issues", new CreateIssueRequest("title-B", "y"));
        await c.PostAsJsonAsync("/api/v1/projects/tk4c/issues", new CreateIssueRequest("title-C", "z"));

        var page = await (await c.GetAsync("/api/v1/projects/tk4c/issues"))
            .Content.ReadFromJsonAsync<PagedResponse<IssueDto>>();
        page!.TotalCount.Should().Be(3);
        page.Data.Select(i => i.Title).Should().BeEquivalentTo(new[] { "title-A", "title-B", "title-C" });
    }
}
