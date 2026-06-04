using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

public class ProjectEndpointsTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public ProjectEndpointsTests(PostgresFixture pg) => _pg = pg;

    [Fact]
    public async Task POST_then_GET_round_trips_a_project()
    {
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();

        var create = await client.PostAsJsonAsync("/api/v1/projects",
            new CreateProjectRequest(Slug: "test-proj-1", Name: "Test Project", Identifier: "TP1"));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await create.Content.ReadFromJsonAsync<ProjectDto>();
        created!.Slug.Should().Be("test-proj-1");
        created.Identifier.Should().Be("TP1");

        var get = await client.GetAsync($"/api/v1/projects/{created.Slug}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await get.Content.ReadFromJsonAsync<ProjectDto>();
        fetched!.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task GET_returns_404_envelope_for_missing_slug()
    {
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();

        var get = await client.GetAsync("/api/v1/projects/does-not-exist");
        get.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var err = await get.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Error.Code.Should().Be("project_not_found");
    }

    [Fact]
    public async Task POST_returns_409_on_duplicate_slug()
    {
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();

        var req = new CreateProjectRequest("dup-slug", "First", "DUP");
        var first = await client.PostAsJsonAsync("/api/v1/projects", req);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync("/api/v1/projects", req);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var err = await second.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Error.Code.Should().Be("project_slug_exists");
    }

    [Fact]
    public async Task GET_states_returns_seeded_workflow_in_sort_order()
    {
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/v1/projects",
            new CreateProjectRequest("kb-proj", "Kanban Project", "KB1"));

        var resp = await client.GetAsync("/api/v1/projects/kb-proj/states");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var states = await resp.Content.ReadFromJsonAsync<List<StateDto>>();
        states.Should().NotBeNull();
        states!.Should().NotBeEmpty();
        states!.Should().BeInAscendingOrder(s => s.SortOrder);
        // Every project gets at least a Backlog state seeded on creation.
        states!.Select(s => s.Group).Should().Contain("Backlog");
    }
}
