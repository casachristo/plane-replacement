using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

public class CommentAnonAuthMutationCoverage : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public CommentAnonAuthMutationCoverage(PostgresFixture pg) => _pg = pg;
    private WaypointApiFactory Anon() => new() { PostgresConnectionString = _pg.ConnectionString, TestPrincipal = null! };

    [Fact]
    public async Task POST_comment_without_auth_returns_401()
    {
        await using var f = Anon(); await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var resp = await c.PostAsJsonAsync("/api/v1/projects/anything/issues/1/comments",
            new CreateCommentRequest("hi"));
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GET_comments_without_auth_returns_401()
    {
        await using var f = Anon(); await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var resp = await c.GetAsync("/api/v1/projects/anything/issues/1/comments");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

public class ProjectAnonAuthMutationCoverage : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public ProjectAnonAuthMutationCoverage(PostgresFixture pg) => _pg = pg;
    private WaypointApiFactory Anon() => new() { PostgresConnectionString = _pg.ConnectionString, TestPrincipal = null! };

    [Fact]
    public async Task GET_list_without_auth_returns_401()
    {
        await using var f = Anon(); await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var resp = await c.GetAsync("/api/v1/projects");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task POST_project_without_auth_returns_401()
    {
        await using var f = Anon(); await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var resp = await c.PostAsJsonAsync("/api/v1/projects",
            new CreateProjectRequest("noauth", "N", "NA"));
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GET_single_without_auth_returns_401()
    {
        await using var f = Anon(); await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var resp = await c.GetAsync("/api/v1/projects/anything");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GET_states_without_auth_returns_401()
    {
        await using var f = Anon(); await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var resp = await c.GetAsync("/api/v1/projects/anything/states");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

public class AcceptanceCriterionAnonAuthMutationCoverage : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public AcceptanceCriterionAnonAuthMutationCoverage(PostgresFixture pg) => _pg = pg;
    private WaypointApiFactory Anon() => new() { PostgresConnectionString = _pg.ConnectionString, TestPrincipal = null! };

    [Fact]
    public async Task GET_AC_list_without_auth_returns_401()
    {
        await using var f = Anon(); await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var resp = await c.GetAsync("/api/v1/projects/anything/issues/1/acceptance-criteria");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task POST_AC_without_auth_returns_401()
    {
        await using var f = Anon(); await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var resp = await c.PostAsJsonAsync(
            "/api/v1/projects/anything/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("x"));
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PATCH_AC_without_auth_returns_401()
    {
        await using var f = Anon(); await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var resp = await c.PatchAsJsonAsync(
            $"/api/v1/projects/anything/issues/1/acceptance-criteria/{Guid.NewGuid()}",
            new UpdateAcceptanceCriterionRequest(Text: "x"));
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task POST_check_without_auth_returns_401()
    {
        await using var f = Anon(); await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var resp = await c.PostAsync(
            $"/api/v1/projects/anything/issues/1/acceptance-criteria/{Guid.NewGuid()}/check", content: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task POST_uncheck_without_auth_returns_401()
    {
        await using var f = Anon(); await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var resp = await c.PostAsync(
            $"/api/v1/projects/anything/issues/1/acceptance-criteria/{Guid.NewGuid()}/uncheck", content: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DELETE_AC_without_auth_returns_401()
    {
        await using var f = Anon(); await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var resp = await c.DeleteAsync(
            $"/api/v1/projects/anything/issues/1/acceptance-criteria/{Guid.NewGuid()}");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
