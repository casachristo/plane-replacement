using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

/// <summary>
/// Additional ProjectEndpoints tests targeting DTO field shape + list behavior.
/// </summary>
public class ProjectEndpointsExtraMutationCoverage : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public ProjectEndpointsExtraMutationCoverage(PostgresFixture pg) => _pg = pg;

    [Fact]
    public async Task GET_list_returns_every_created_project_by_slug()
    {
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("plist-a", "A", "PLA"));
        await client.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("plist-b", "B", "PLB"));

        var resp = await client.GetAsync("/api/v1/projects");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await resp.Content.ReadFromJsonAsync<List<ProjectDto>>();
        list!.Select(p => p.Slug).Should().Contain("plist-a").And.Contain("plist-b");
    }

    [Fact]
    public async Task GET_single_project_returns_DTO_with_matching_identifier_and_name()
    {
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("pget", "Get Project Name", "PGET"));

        var resp = await client.GetAsync("/api/v1/projects/pget");
        var dto = await resp.Content.ReadFromJsonAsync<ProjectDto>();
        dto!.Identifier.Should().Be("PGET");
        dto.Name.Should().Be("Get Project Name");
        dto.Slug.Should().Be("pget");
    }

    [Fact]
    public async Task POST_project_returns_Created_with_DTO_having_assigned_Id()
    {
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/v1/projects",
            new CreateProjectRequest("pcreate", "Create Project", "PCR"));
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await resp.Content.ReadFromJsonAsync<ProjectDto>();
        dto!.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task POST_project_returns_Location_header_pointing_to_new_resource()
    {
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/v1/projects",
            new CreateProjectRequest("ploc", "Located", "PLOC"));
        resp.Headers.Location?.OriginalString.Should().EndWith("/ploc");
    }
}
