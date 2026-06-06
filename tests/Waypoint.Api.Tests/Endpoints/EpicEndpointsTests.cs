using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

public class EpicEndpointsTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public EpicEndpointsTests(PostgresFixture pg) => _pg = pg;

    // Unique slug per test — the Postgres fixture persists across tests AND runs.
    private async Task<(HttpClient client, string slug)> NewProject()
    {
        var slug = "ep-" + Guid.NewGuid().ToString("N")[..12];
        var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest(slug, slug, slug[3..6].ToUpperInvariant()));
        return (client, slug);
    }

    [Fact]
    public async Task Create_then_list_epics_in_sequence_order()
    {
        var (c, slug) = await NewProject();

        var a = await c.PostAsJsonAsync($"/api/v1/projects/{slug}/epics", new CreateEpicRequest("Auth"));
        a.StatusCode.Should().Be(HttpStatusCode.Created);
        (await a.Content.ReadFromJsonAsync<EpicDto>())!.Sequence.Should().Be(1);

        await c.PostAsJsonAsync($"/api/v1/projects/{slug}/epics", new CreateEpicRequest("Billing"));

        var list = (await c.GetFromJsonAsync<List<EpicDto>>($"/api/v1/projects/{slug}/epics"))!;
        list.Select(e => e.Title).Should().Equal("Auth", "Billing");
        list.Select(e => e.Sequence).Should().Equal(1, 2);
    }

    [Fact]
    public async Task Empty_epic_title_is_rejected()
    {
        var (c, slug) = await NewProject();
        var resp = await c.PostAsJsonAsync($"/api/v1/projects/{slug}/epics", new CreateEpicRequest("  "));
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);   // ValidationException -> 422
    }

    [Fact]
    public async Task Assign_issue_to_an_epic_then_unassign()
    {
        var (c, slug) = await NewProject();
        var epic = await (await c.PostAsJsonAsync($"/api/v1/projects/{slug}/epics", new CreateEpicRequest("Auth")))
            .Content.ReadFromJsonAsync<EpicDto>();
        await c.PostAsJsonAsync($"/api/v1/projects/{slug}/issues", new CreateIssueRequest("Login bug", ""));

        var assigned = await c.PutAsJsonAsync($"/api/v1/projects/{slug}/issues/1/epic", new AssignEpicRequest(epic!.Id));
        assigned.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await assigned.Content.ReadFromJsonAsync<IssueDto>();
        dto!.EpicId.Should().Be(epic.Id);
        dto.EpicTitle.Should().Be("Auth");

        var unassigned = await c.PutAsJsonAsync($"/api/v1/projects/{slug}/issues/1/epic", new AssignEpicRequest(null));
        (await unassigned.Content.ReadFromJsonAsync<IssueDto>())!.EpicId.Should().BeNull();
    }

    [Fact]
    public async Task Assigning_an_epic_from_another_project_is_rejected()
    {
        var (c, slug) = await NewProject();
        await c.PostAsJsonAsync($"/api/v1/projects/{slug}/issues", new CreateIssueRequest("X", ""));
        var resp = await c.PutAsJsonAsync($"/api/v1/projects/{slug}/issues/1/epic",
            new AssignEpicRequest(Guid.NewGuid()));   // non-existent epic
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
