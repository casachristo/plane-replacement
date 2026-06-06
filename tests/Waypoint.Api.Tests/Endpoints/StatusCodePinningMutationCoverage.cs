using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

/// <summary>
/// Status-code pinning: kills mutations that change the HTTP status code
/// returned by each endpoint (e.g. Results.Created → Results.Ok, 201 → 200).
/// </summary>
public class StatusCodePinningMutationCoverage : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public StatusCodePinningMutationCoverage(PostgresFixture pg) => _pg = pg;

    private async Task<HttpClient> NewClient(string slug, string ident)
    {
        var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        var c = f.CreateClient();
        await c.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest(slug, "p", ident));
        return c;
    }

    [Fact]
    public async Task POST_project_returns_201_Created()
    {
        await using var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var resp = await c.PostAsJsonAsync("/api/v1/projects",
            new CreateProjectRequest("scp1", "P", "SCP1"));
        ((int)resp.StatusCode).Should().Be(201);
    }

    [Fact]
    public async Task GET_project_returns_200_OK()
    {
        using var c = await NewClient("scp2", "SCP2");
        var resp = await c.GetAsync("/api/v1/projects/scp2");
        ((int)resp.StatusCode).Should().Be(200);
    }

    [Fact]
    public async Task POST_issue_returns_201_Created()
    {
        using var c = await NewClient("scp3", "SCP3");
        var resp = await c.PostAsJsonAsync("/api/v1/projects/scp3/issues",
            new CreateIssueRequest("t", "b"));
        ((int)resp.StatusCode).Should().Be(201);
    }

    [Fact]
    public async Task GET_issue_returns_200_OK()
    {
        using var c = await NewClient("scp4", "SCP4");
        await c.PostAsJsonAsync("/api/v1/projects/scp4/issues", new CreateIssueRequest("t", "b"));
        var resp = await c.GetAsync("/api/v1/projects/scp4/issues/1");
        ((int)resp.StatusCode).Should().Be(200);
    }

    [Fact]
    public async Task PATCH_issue_returns_200_OK()
    {
        using var c = await NewClient("scp5", "SCP5");
        await c.PostAsJsonAsync("/api/v1/projects/scp5/issues", new CreateIssueRequest("t", "b"));
        var resp = await c.PatchAsJsonAsync("/api/v1/projects/scp5/issues/1",
            new UpdateIssueRequest(Title: "x"));
        ((int)resp.StatusCode).Should().Be(200);
    }

    [Fact]
    public async Task POST_AC_returns_201_Created()
    {
        using var c = await NewClient("scp6", "SCP6");
        await c.PostAsJsonAsync("/api/v1/projects/scp6/issues", new CreateIssueRequest("t", "b"));
        var resp = await c.PostAsJsonAsync(
            "/api/v1/projects/scp6/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("x"));
        ((int)resp.StatusCode).Should().Be(201);
    }

    [Fact]
    public async Task DELETE_AC_returns_204_NoContent()
    {
        using var c = await NewClient("scp7", "SCP7");
        await c.PostAsJsonAsync("/api/v1/projects/scp7/issues", new CreateIssueRequest("t", "b"));
        var ac = await (await c.PostAsJsonAsync(
            "/api/v1/projects/scp7/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("x"))).Content.ReadFromJsonAsync<AcceptanceCriterionDto>();
        var resp = await c.DeleteAsync(
            $"/api/v1/projects/scp7/issues/1/acceptance-criteria/{ac!.Id}");
        ((int)resp.StatusCode).Should().Be(204);
    }

    [Fact]
    public async Task POST_comment_returns_201_Created()
    {
        using var c = await NewClient("scp8", "SCP8");
        await c.PostAsJsonAsync("/api/v1/projects/scp8/issues", new CreateIssueRequest("t", "b"));
        var resp = await c.PostAsJsonAsync("/api/v1/projects/scp8/issues/1/comments",
            new CreateCommentRequest("hi"));
        ((int)resp.StatusCode).Should().Be(201);
    }
}
