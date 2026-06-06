using System.Net.Http.Json;
using FluentAssertions;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

public class TargetedKills3MutationCoverage : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public TargetedKills3MutationCoverage(PostgresFixture pg) => _pg = pg;

    [Fact]
    public async Task Issue_GET_wrong_project_returns_404_not_a_different_project_issue()
    {
        // Kills the i.ProjectId == project.Id && i.SequenceId == seq mutations
        // (replace && with ||). With ||, GETting issue 1 from project B would
        // return project A's issue 1.
        await using var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        await c.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("tk3a", "P A", "TK3A"));
        await c.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("tk3b", "P B", "TK3B"));
        await c.PostAsJsonAsync("/api/v1/projects/tk3a/issues", new CreateIssueRequest("a-issue", "x"));
        // tk3b has NO issues. GET tk3b/issues/1 must be 404 (not return tk3a's issue 1).
        var resp = await c.GetAsync("/api/v1/projects/tk3b/issues/1");
        ((int)resp.StatusCode).Should().Be(404);
    }

    [Fact]
    public async Task Issue_activity_for_wrong_project_returns_404()
    {
        await using var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        await c.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("tk3c", "P C", "TK3C"));
        await c.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("tk3d", "P D", "TK3D"));
        await c.PostAsJsonAsync("/api/v1/projects/tk3c/issues", new CreateIssueRequest("x", "y"));
        var resp = await c.GetAsync("/api/v1/projects/tk3d/issues/1/activity");
        ((int)resp.StatusCode).Should().Be(404);
    }

    [Fact]
    public async Task Issue_GET_list_TotalCount_is_zero_for_fresh_project()
    {
        await using var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        await c.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("tk3e", "p", "TK3E"));
        var page = await (await c.GetAsync("/api/v1/projects/tk3e/issues"))
            .Content.ReadFromJsonAsync<PagedResponse<IssueDto>>();
        page!.TotalCount.Should().Be(0);
        page.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task Transition_to_same_state_does_NOT_record_a_transitioned_activity()
    {
        // The TransitionAsync repo short-circuits when StateId == toStateId.
        // No activity should be added.
        await using var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        await c.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("tk3f", "p", "TK3F"));
        await c.PostAsJsonAsync("/api/v1/projects/tk3f/issues", new CreateIssueRequest("t", "b"));
        var issue = await (await c.GetAsync("/api/v1/projects/tk3f/issues/1"))
            .Content.ReadFromJsonAsync<IssueDto>();

        await c.PostAsJsonAsync("/api/v1/projects/tk3f/issues/1/transitions",
            new TransitionIssueRequest(issue!.StateId));

        var events = await (await c.GetAsync("/api/v1/projects/tk3f/issues/1/activity"))
            .Content.ReadFromJsonAsync<List<ActivityDto>>();
        events!.Any(e => e.Verb == "transitioned").Should().BeFalse();
    }
}
