using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

public class IssueEndpointsTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public IssueEndpointsTests(PostgresFixture pg) => _pg = pg;

    [Fact]
    public async Task POST_creates_issue_with_seq_1_then_2()
    {
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/v1/projects",
            new CreateProjectRequest("issues-proj", "Issues Project", "ISP"));

        var first = await client.PostAsJsonAsync("/api/v1/projects/issues-proj/issues",
            new CreateIssueRequest("First", "Body **md**"));
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstDto = await first.Content.ReadFromJsonAsync<IssueDto>();
        firstDto!.Sequence.Should().Be(1);

        var second = await client.PostAsJsonAsync("/api/v1/projects/issues-proj/issues",
            new CreateIssueRequest("Second", "Body 2"));
        var secondDto = await second.Content.ReadFromJsonAsync<IssueDto>();
        secondDto!.Sequence.Should().Be(2);
    }

    [Fact]
    public async Task GET_by_sequence_returns_issue()
    {
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/v1/projects",
            new CreateProjectRequest("get-proj", "Get Project", "GP1"));
        await client.PostAsJsonAsync("/api/v1/projects/get-proj/issues",
            new CreateIssueRequest("Hello", "World"));

        var get = await client.GetAsync("/api/v1/projects/get-proj/issues/1");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await get.Content.ReadFromJsonAsync<IssueDto>();
        dto!.Title.Should().Be("Hello");
        dto.DescriptionMd.Should().Be("World");
        dto.StateName.Should().Be("To Do");
    }

    [Fact]
    public async Task GET_returns_404_for_missing_sequence()
    {
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/v1/projects",
            new CreateProjectRequest("nf-proj", "NF Project", "NF1"));
        var get = await client.GetAsync("/api/v1/projects/nf-proj/issues/9999");
        get.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var err = await get.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Error.Code.Should().Be("issue_not_found");
    }

    [Fact]
    public async Task PATCH_updates_title_only_when_only_title_provided()
    {
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("patch-proj", "P", "PP1"));
        var issue = await (await client.PostAsJsonAsync("/api/v1/projects/patch-proj/issues",
            new CreateIssueRequest("Old", "Body unchanged"))).Content.ReadFromJsonAsync<IssueDto>();

        var resp = await client.PatchAsJsonAsync($"/api/v1/projects/patch-proj/issues/{issue!.Sequence}",
            new UpdateIssueRequest(Title: "New"));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await resp.Content.ReadFromJsonAsync<IssueDto>();
        updated!.Title.Should().Be("New");
        updated.DescriptionMd.Should().Be("Body unchanged");
    }

    [Fact]
    public async Task POST_transition_to_state_succeeds_when_workflow_allows()
    {
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/v1/projects",
            new CreateProjectRequest("trans-proj", "T", "TRP"));
        Guid inProgressId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Waypoint.Domain.WaypointDbContext>();
            var project = db.Projects.Single(p => p.Slug == "trans-proj");
            // The default workflow already provides To Do / In Progress / Done; reuse the
            // existing In Progress state (state names are unique per project) and just wire
            // a transition into it from the default landing state.
            var inProgress = db.States.Single(s => s.ProjectId == project.Id && s.Name == "In Progress");
            var workflow = db.Workflows.Single(w => w.ProjectId == project.Id);
            db.WorkflowTransitions.Add(new Waypoint.Domain.Entities.WorkflowTransition
            {
                WorkflowId = workflow.Id,
                FromStateId = project.DefaultStateId!.Value,
                ToStateId = inProgress.Id,
            });
            db.SaveChanges();
            inProgressId = inProgress.Id;
        }

        var issue = await (await client.PostAsJsonAsync("/api/v1/projects/trans-proj/issues",
            new CreateIssueRequest("T1", "body"))).Content.ReadFromJsonAsync<IssueDto>();

        var resp = await client.PostAsJsonAsync($"/api/v1/projects/trans-proj/issues/{issue!.Sequence}/transitions",
            new TransitionIssueRequest(inProgressId));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await resp.Content.ReadFromJsonAsync<IssueDto>();
        updated!.StateName.Should().Be("In Progress");
    }

    [Fact]
    public async Task POST_transition_returns_409_when_workflow_disallows()
    {
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/v1/projects",
            new CreateProjectRequest("bad-trans", "BT", "BTP"));
        Guid otherId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Waypoint.Domain.WaypointDbContext>();
            var project = db.Projects.Single(p => p.Slug == "bad-trans");
            // Reuse the auto-created Done state. No To Do -> Done transition exists in the
            // default workflow, so the transition must be rejected with 409.
            var other = db.States.Single(s => s.ProjectId == project.Id && s.Name == "Done");
            otherId = other.Id;
        }

        var issue = await (await client.PostAsJsonAsync("/api/v1/projects/bad-trans/issues",
            new CreateIssueRequest("Will fail", ""))).Content.ReadFromJsonAsync<IssueDto>();

        var resp = await client.PostAsJsonAsync($"/api/v1/projects/bad-trans/issues/{issue!.Sequence}/transitions",
            new TransitionIssueRequest(otherId));
        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Error.Code.Should().Be("transition_not_allowed");
    }

    [Fact]
    public async Task GET_list_returns_page_of_issues_with_next_cursor()
    {
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/v1/projects",
            new CreateProjectRequest("list-proj", "L", "LP1"));
        for (var i = 0; i < 5; i++)
        {
            await client.PostAsJsonAsync("/api/v1/projects/list-proj/issues",
                new CreateIssueRequest($"Issue {i}", "body"));
        }

        var resp = await client.GetAsync("/api/v1/projects/list-proj/issues?limit=3");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await resp.Content.ReadFromJsonAsync<PagedResponse<IssueDto>>();
        page!.Data.Should().HaveCount(3);
        page.NextCursor.Should().NotBeNullOrEmpty();
        page.TotalCount.Should().Be(5);

        var resp2 = await client.GetAsync($"/api/v1/projects/list-proj/issues?limit=3&cursor={Uri.EscapeDataString(page.NextCursor!)}");
        var page2 = await resp2.Content.ReadFromJsonAsync<PagedResponse<IssueDto>>();
        page2!.Data.Should().HaveCount(2);
        page2.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task GET_activity_returns_creation_event()
    {
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("act-proj", "A", "AP1"));
        var issue = await (await client.PostAsJsonAsync("/api/v1/projects/act-proj/issues",
            new CreateIssueRequest("X", ""))).Content.ReadFromJsonAsync<IssueDto>();

        var resp = await client.GetAsync($"/api/v1/projects/act-proj/issues/{issue!.Sequence}/activity");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var events = await resp.Content.ReadFromJsonAsync<List<ActivityDto>>();
        events!.Should().NotBeEmpty();
        events.Should().Contain(e => e.Verb == "created");
    }

    [Fact]
    public async Task POST_creates_issue_assigned_to_epic_and_cycle()
    {
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("ec-proj", "EC", "ECT"));

        Guid epicId, cycleId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Waypoint.Domain.WaypointDbContext>();
            var project = db.Projects.Single(p => p.Slug == "ec-proj");
            var epic = new Waypoint.Domain.Entities.Epic { ProjectId = project.Id, SequenceId = 1, Title = "Arch Studio" };
            var cycle = new Waypoint.Domain.Entities.Cycle { ProjectId = project.Id, Name = "Sprint 1", StartDate = DateTimeOffset.UtcNow, EndDate = DateTimeOffset.UtcNow.AddDays(14) };
            db.Add(epic); db.Add(cycle); db.SaveChanges();
            epicId = epic.Id; cycleId = cycle.Id;
        }

        var resp = await client.PostAsJsonAsync("/api/v1/projects/ec-proj/issues",
            new CreateIssueRequest("Tagged", "body", EpicId: epicId, CycleId: cycleId));
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await resp.Content.ReadFromJsonAsync<IssueDto>();
        dto!.EpicId.Should().Be(epicId);
        dto.EpicTitle.Should().Be("Arch Studio");
        dto.CycleId.Should().Be(cycleId);
        dto.CycleName.Should().Be("Sprint 1");
    }

    [Fact]
    public async Task POST_returns_404_when_epic_not_in_project()
    {
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("ep404-proj", "E4", "EPF"));

        var resp = await client.PostAsJsonAsync("/api/v1/projects/ep404-proj/issues",
            new CreateIssueRequest("Bad epic", "body", EpicId: Guid.NewGuid()));
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task POST_returns_404_when_cycle_not_in_project()
    {
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("cy404-proj", "C4", "CYF"));

        var resp = await client.PostAsJsonAsync("/api/v1/projects/cy404-proj/issues",
            new CreateIssueRequest("Bad cycle", "body", CycleId: Guid.NewGuid()));
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PUT_cycle_assigns_then_unassigns()
    {
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("pc-proj", "PC", "PCY"));

        Guid cycleId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Waypoint.Domain.WaypointDbContext>();
            var project = db.Projects.Single(p => p.Slug == "pc-proj");
            var cycle = new Waypoint.Domain.Entities.Cycle { ProjectId = project.Id, Name = "M1", StartDate = DateTimeOffset.UtcNow, EndDate = DateTimeOffset.UtcNow.AddDays(7) };
            db.Add(cycle); db.SaveChanges();
            cycleId = cycle.Id;
        }
        var issue = await (await client.PostAsJsonAsync("/api/v1/projects/pc-proj/issues",
            new CreateIssueRequest("C1", "body"))).Content.ReadFromJsonAsync<IssueDto>();

        var assign = await client.PutAsJsonAsync($"/api/v1/projects/pc-proj/issues/{issue!.Sequence}/cycle",
            new AssignCycleRequest(cycleId));
        assign.StatusCode.Should().Be(HttpStatusCode.OK);
        (await assign.Content.ReadFromJsonAsync<IssueDto>())!.CycleId.Should().Be(cycleId);

        var unassign = await client.PutAsJsonAsync($"/api/v1/projects/pc-proj/issues/{issue.Sequence}/cycle",
            new AssignCycleRequest(null));
        unassign.StatusCode.Should().Be(HttpStatusCode.OK);
        (await unassign.Content.ReadFromJsonAsync<IssueDto>())!.CycleId.Should().BeNull();
    }

    [Fact]
    public async Task PUT_cycle_returns_404_for_unknown_cycle()
    {
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("uc-proj", "UC", "UCY"));
        var issue = await (await client.PostAsJsonAsync("/api/v1/projects/uc-proj/issues",
            new CreateIssueRequest("U1", "body"))).Content.ReadFromJsonAsync<IssueDto>();

        var resp = await client.PutAsJsonAsync($"/api/v1/projects/uc-proj/issues/{issue!.Sequence}/cycle",
            new AssignCycleRequest(Guid.NewGuid()));
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
