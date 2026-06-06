using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

public class IssueAnonAuthExtraMutationCoverage : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public IssueAnonAuthExtraMutationCoverage(PostgresFixture pg) => _pg = pg;

    private WaypointApiFactory AnonFactory() => new()
    {
        PostgresConnectionString = _pg.ConnectionString,
        TestPrincipal = null!,   // FixedPrincipalResolver returns null → AuthGuard.RequireAuth throws 401
    };

    [Fact]
    public async Task GET_issue_without_auth_returns_401()
    {
        await using var f = AnonFactory();
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var resp = await c.GetAsync("/api/v1/projects/anything/issues/1");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task POST_issue_without_auth_returns_401()
    {
        await using var f = AnonFactory();
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var resp = await c.PostAsJsonAsync("/api/v1/projects/anything/issues",
            new CreateIssueRequest("t", "b"));
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PATCH_issue_without_auth_returns_401()
    {
        await using var f = AnonFactory();
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var resp = await c.PatchAsJsonAsync("/api/v1/projects/anything/issues/1",
            new UpdateIssueRequest(Title: "x"));
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task POST_transition_without_auth_returns_401()
    {
        await using var f = AnonFactory();
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var resp = await c.PostAsJsonAsync("/api/v1/projects/anything/issues/1/transitions",
            new TransitionIssueRequest(Guid.NewGuid()));
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GET_list_without_auth_returns_401()
    {
        await using var f = AnonFactory();
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var resp = await c.GetAsync("/api/v1/projects/anything/issues");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GET_activity_without_auth_returns_401()
    {
        await using var f = AnonFactory();
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var resp = await c.GetAsync("/api/v1/projects/anything/issues/1/activity");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Issue_not_found_error_message_includes_identifier_and_seq()
    {
        await using var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        await c.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("imsg", "p", "IMSG"));
        var resp = await c.GetAsync("/api/v1/projects/imsg/issues/42");
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Error.Message.Should().Contain("IMSG").And.Contain("42");
    }

    [Fact]
    public async Task Project_not_found_error_message_includes_slug()
    {
        await using var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var resp = await c.PostAsJsonAsync("/api/v1/projects/unknown-slug-12345/issues",
            new CreateIssueRequest("t", "b"));
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Error.Message.Should().Contain("unknown-slug-12345");
    }
}
