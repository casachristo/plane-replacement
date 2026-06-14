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
        // No backlog: new projects seed a To Do / In Progress / Done workflow, To Do default.
        states!.Select(s => s.Name).Should().Equal("To Do", "In Progress", "Done");
        states!.Select(s => s.Group).Should().Equal("Unstarted", "Started", "Completed");
        states!.Should().NotContain(s => s.Group == "Backlog");
        states!.Single(s => s.IsDefault).Name.Should().Be("To Do");
    }

    [Fact]
    public async Task GET_states_returns_404_for_missing_slug()
    {
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();

        var resp = await client.GetAsync("/api/v1/projects/no-such-proj/states");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await resp.Content.ReadFromJsonAsync<ErrorResponse>())!.Error.Code.Should().Be("project_not_found");
    }

    [Fact]
    public async Task GET_labels_is_empty_for_a_new_project_and_404s_for_a_missing_one()
    {
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("lbl-proj", "Labels", "LBL"));

        var ok = await client.GetAsync("/api/v1/projects/lbl-proj/labels");
        ok.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ok.Content.ReadFromJsonAsync<List<LabelDto>>())!.Should().BeEmpty();

        var missing = await client.GetAsync("/api/v1/projects/no-such-proj/labels");
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await missing.Content.ReadFromJsonAsync<ErrorResponse>())!.Error.Code.Should().Be("project_not_found");
    }

    [Fact]
    public async Task PUT_cairn_link_trims_the_name_and_swimlanes_reflect_it_immediately()
    {
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("lnk-proj", "Linked", "LNK"));

        // Read swimlanes first so the slug cache is populated as "unlinked"; the link must still
        // be visible immediately afterwards (the Service invalidates the cache on write).
        (await (await client.GetAsync("/api/v1/projects/lnk-proj/swimlanes"))
            .Content.ReadFromJsonAsync<SwimlanesDto>())!.CairnLinked.Should().BeFalse();

        var link = await client.PutAsJsonAsync("/api/v1/projects/lnk-proj/cairn-link",
            new SetCairnLinkRequest("  Waypoint  "));
        link.StatusCode.Should().Be(HttpStatusCode.OK);
        (await link.Content.ReadFromJsonAsync<ProjectDto>())!.CairnProjectName.Should().Be("Waypoint");

        var swim = await (await client.GetAsync("/api/v1/projects/lnk-proj/swimlanes"))
            .Content.ReadFromJsonAsync<SwimlanesDto>();
        swim!.CairnLinked.Should().BeTrue();
    }
}
