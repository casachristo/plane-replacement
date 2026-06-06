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
        var firstEpic = (await a.Content.ReadFromJsonAsync<EpicDto>())!;
        firstEpic.Sequence.Should().Be(1);
        firstEpic.Status.Should().Be("planned");   // default status literal

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
        var verr = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        verr!.Error.Code.Should().Be("title_required");
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
    // Two projects sharing ONE database, so an epic in project B is visible while
    // operating on project A -- needed to exercise the cross-project guards.
    private async Task<(HttpClient client, string slugA, string slugB)> TwoProjects()
    {
        var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        var client = factory.CreateClient();
        var a = "ea-" + Guid.NewGuid().ToString("N")[..10];
        var b = "eb-" + Guid.NewGuid().ToString("N")[..10];
        await client.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest(a, a, a[3..6].ToUpperInvariant()));
        await client.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest(b, b, b[3..6].ToUpperInvariant()));
        return (client, a, b);
    }

    [Fact]
    public async Task Assigning_an_epic_that_belongs_to_another_project_is_rejected()
    {
        var (c, slugA, slugB) = await TwoProjects();
        // Epic created under project B...
        var epicB = await (await c.PostAsJsonAsync($"/api/v1/projects/{slugB}/epics", new CreateEpicRequest("B-epic")))
            .Content.ReadFromJsonAsync<EpicDto>();
        await c.PostAsJsonAsync($"/api/v1/projects/{slugA}/issues", new CreateIssueRequest("A-issue", ""));
        // ...must NOT be assignable to an issue in project A. Kills the
        // e.Id == epicId && e.ProjectId == project.Id guard: a mutated || would
        // match B-epic by id alone and let the assignment through.
        var resp = await c.PutAsJsonAsync($"/api/v1/projects/{slugA}/issues/1/epic", new AssignEpicRequest(epicB!.Id));
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await resp.Content.ReadFromJsonAsync<ErrorResponse>())!.Error.Code.Should().Be("epic_not_found");
    }

    [Fact]
    public async Task Assigning_a_module_to_a_missing_issue_returns_404()
    {
        var (c, slugA, slugB) = await TwoProjects();
        // Project A has issue seq 1; project B has none. Unassigning on project B
        // issue 1 (which does not exist) must 404 -- kills both the ?? throw and the
        // i.ProjectId == project.Id && i.SequenceId == seq guard (a mutated || would
        // wrongly resolve project A issue 1).
        await c.PostAsJsonAsync($"/api/v1/projects/{slugA}/issues", new CreateIssueRequest("only in A", ""));
        var resp = await c.PutAsJsonAsync($"/api/v1/projects/{slugB}/issues/1/epic", new AssignEpicRequest(null));
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await resp.Content.ReadFromJsonAsync<ErrorResponse>())!.Error.Code.Should().Be("issue_not_found");
    }

    [Fact]
    public async Task Listing_epics_for_a_missing_project_returns_404()
    {
        var (c, _) = await NewProject();
        var resp = await c.GetAsync($"/api/v1/projects/missing-{Guid.NewGuid():N}/epics");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Creating_an_epic_under_a_missing_project_returns_404()
    {
        var (c, _) = await NewProject();
        var resp = await c.PostAsJsonAsync($"/api/v1/projects/missing-{Guid.NewGuid():N}/epics",
            new CreateEpicRequest("X"));
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
