using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

/// <summary>
/// Edge / negative tests for AcceptanceCriterionEndpoints, exercising the
/// not-found and validation paths that the happy-path tests in
/// AcceptanceCriterionEndpointsTests don't reach.
/// </summary>
public class AcceptanceCriterionEndpointsExtraMutationCoverage : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public AcceptanceCriterionEndpointsExtraMutationCoverage(PostgresFixture pg) => _pg = pg;

    private async Task<HttpClient> NewClient(string slug, string ident)
    {
        var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        var c = f.CreateClient();
        await c.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest(slug, "p", ident));
        await c.PostAsJsonAsync($"/api/v1/projects/{slug}/issues",
            new CreateIssueRequest("first", "body"));
        return c;
    }

    [Fact]
    public async Task POST_AC_against_unknown_project_returns_404_project_not_found()
    {
        using var client = await NewClient("ace1", "ACE1");
        var resp = await client.PostAsJsonAsync(
            "/api/v1/projects/does-not-exist/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("text"));
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Error.Code.Should().Be("project_not_found");
    }

    [Fact]
    public async Task POST_AC_against_unknown_issue_returns_404_issue_not_found()
    {
        using var client = await NewClient("ace2", "ACE2");
        var resp = await client.PostAsJsonAsync(
            "/api/v1/projects/ace2/issues/9999/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("text"));
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Error.Code.Should().Be("issue_not_found");
    }

    [Fact]
    public async Task PATCH_unknown_AC_returns_404_ac_not_found()
    {
        using var client = await NewClient("ace3", "ACE3");
        var resp = await client.PatchAsJsonAsync(
            $"/api/v1/projects/ace3/issues/1/acceptance-criteria/{Guid.NewGuid()}",
            new UpdateAcceptanceCriterionRequest(Text: "x"));
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Error.Code.Should().Be("ac_not_found");
    }

    [Fact]
    public async Task PATCH_AC_with_whitespace_text_returns_422_text_required()
    {
        using var client = await NewClient("ace4", "ACE4");
        var created = await (await client.PostAsJsonAsync(
            "/api/v1/projects/ace4/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("ok"))).Content.ReadFromJsonAsync<AcceptanceCriterionDto>();
        var resp = await client.PatchAsJsonAsync(
            $"/api/v1/projects/ace4/issues/1/acceptance-criteria/{created!.Id}",
            new UpdateAcceptanceCriterionRequest(Text: "   "));
        ((int)resp.StatusCode).Should().Be(422);
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Error.Code.Should().Be("text_required");
    }

    [Fact]
    public async Task POST_check_on_unknown_AC_returns_404()
    {
        using var client = await NewClient("ace5", "ACE5");
        var resp = await client.PostAsync(
            $"/api/v1/projects/ace5/issues/1/acceptance-criteria/{Guid.NewGuid()}/check", content: null);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Error.Code.Should().Be("ac_not_found");
    }

    [Fact]
    public async Task POST_uncheck_on_unknown_AC_returns_404()
    {
        using var client = await NewClient("ace6", "ACE6");
        var resp = await client.PostAsync(
            $"/api/v1/projects/ace6/issues/1/acceptance-criteria/{Guid.NewGuid()}/uncheck", content: null);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Error.Code.Should().Be("ac_not_found");
    }

    [Fact]
    public async Task DELETE_unknown_AC_returns_404()
    {
        using var client = await NewClient("ace7", "ACE7");
        var resp = await client.DeleteAsync(
            $"/api/v1/projects/ace7/issues/1/acceptance-criteria/{Guid.NewGuid()}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Error.Code.Should().Be("ac_not_found");
    }

    [Fact]
    public async Task POST_check_returns_specific_actor_type_User_for_Human_principal()
    {
        // Kills the ternary mutations on ResolveActor's Kind == Human branch:
        // pin the value to "User", not just "non-empty".
        using var client = await NewClient("ace8", "ACE8");
        var ac = await (await client.PostAsJsonAsync(
            "/api/v1/projects/ace8/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("test"))).Content.ReadFromJsonAsync<AcceptanceCriterionDto>();
        var resp = await client.PostAsync(
            $"/api/v1/projects/ace8/issues/1/acceptance-criteria/{ac!.Id}/check", content: null);
        var checkedDto = await resp.Content.ReadFromJsonAsync<AcceptanceCriterionDto>();
        checkedDto!.CheckedByActorType.Should().Be("User");
    }
}
