using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

/// <summary>
/// Mutation-coverage tests for IssueEndpoints' edge paths — beyond the happy
/// paths in IssueEndpointsTests. Hits each not-found / wrong-project response
/// envelope to kill string mutations on error codes + messages.
/// </summary>
public class IssueEndpointsExtraMutationCoverage : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public IssueEndpointsExtraMutationCoverage(PostgresFixture pg) => _pg = pg;

    private async Task<HttpClient> NewClient(string slug, string ident)
    {
        var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        var c = f.CreateClient();
        await c.PostAsJsonAsync("/api/v1/projects",
            new CreateProjectRequest(slug, "p", ident));
        await c.PostAsJsonAsync($"/api/v1/projects/{slug}/issues",
            new CreateIssueRequest("First", "body"));
        return c;
    }

    [Fact]
    public async Task PATCH_unknown_issue_returns_404_issue_not_found()
    {
        using var client = await NewClient("ix1", "IX1");
        var resp = await client.PatchAsJsonAsync("/api/v1/projects/ix1/issues/9999",
            new UpdateIssueRequest(Title: "x"));
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Error.Code.Should().Be("issue_not_found");
    }

    [Fact]
    public async Task PATCH_against_unknown_project_returns_404_project_not_found()
    {
        using var client = await NewClient("ix2", "IX2");
        var resp = await client.PatchAsJsonAsync("/api/v1/projects/does-not-exist/issues/1",
            new UpdateIssueRequest(Title: "x"));
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Error.Code.Should().Be("project_not_found");
    }

    [Fact]
    public async Task POST_transition_with_unknown_to_state_returns_404_state_not_found()
    {
        using var client = await NewClient("ix3", "IX3");
        var resp = await client.PostAsJsonAsync("/api/v1/projects/ix3/issues/1/transitions",
            new TransitionIssueRequest(Guid.NewGuid()));   // random state id
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Error.Code.Should().Be("state_not_found");
    }

    [Fact]
    public async Task POST_transition_against_unknown_project_returns_404_project_not_found()
    {
        using var client = await NewClient("ix4", "IX4");
        var resp = await client.PostAsJsonAsync("/api/v1/projects/does-not-exist/issues/1/transitions",
            new TransitionIssueRequest(Guid.NewGuid()));
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Error.Code.Should().Be("project_not_found");
    }

    [Fact]
    public async Task GET_activity_for_unknown_issue_returns_404_issue_not_found()
    {
        using var client = await NewClient("ix5", "IX5");
        var resp = await client.GetAsync("/api/v1/projects/ix5/issues/9999/activity");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Error.Code.Should().Be("issue_not_found");
    }

    [Fact]
    public async Task GET_activity_against_unknown_project_returns_404_project_not_found()
    {
        using var client = await NewClient("ix6", "IX6");
        var resp = await client.GetAsync("/api/v1/projects/does-not-exist/issues/1/activity");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Error.Code.Should().Be("project_not_found");
    }

    [Fact]
    public async Task POST_issue_against_unknown_project_returns_404_project_not_found()
    {
        using var client = await NewClient("ix7", "IX7");
        var resp = await client.PostAsJsonAsync("/api/v1/projects/does-not-exist/issues",
            new CreateIssueRequest("title", "body"));
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Error.Code.Should().Be("project_not_found");
    }

    [Fact]
    public async Task GET_list_against_unknown_project_returns_404_project_not_found()
    {
        using var client = await NewClient("ix8", "IX8");
        var resp = await client.GetAsync("/api/v1/projects/does-not-exist/issues");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Error.Code.Should().Be("project_not_found");
    }
}
