using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Waypoint.Domain;
using Waypoint.Domain.Entities;
using Waypoint.Domain.Enums;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

/// <summary>
/// WAY-4: server-side gate that refuses to transition an issue into a Completed-group
/// state while any of its acceptance criteria are unchecked. Bypass with force=true.
/// </summary>
public class TransitionGateTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public TransitionGateTests(PostgresFixture pg) => _pg = pg;

    private static async Task<(HttpClient client, Guid issueId, int issueSeq, Guid doneStateId)>
        SetupProjectIssueAndDone(PostgresFixture pg, string slug, string ident)
    {
        var factory = new WaypointApiFactory { PostgresConnectionString = pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/v1/projects",
            new CreateProjectRequest(slug, $"P {slug}", ident));

        Guid doneId;
        Guid issueDbId;
        int issueSeq;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WaypointDbContext>();
            var project = db.Projects.Single(p => p.Slug == slug);
            var done = new State
            {
                ProjectId = project.Id,
                Name = "Done",
                Group = StateGroup.Completed,
                Color = "#22c55e",
                SortOrder = 1,
            };
            db.States.Add(done);
            db.SaveChanges();
            doneId = done.Id;

            var workflow = db.Workflows.Single(w => w.ProjectId == project.Id);
            db.WorkflowTransitions.Add(new WorkflowTransition
            {
                WorkflowId = workflow.Id,
                FromStateId = project.DefaultStateId!.Value,
                ToStateId = done.Id,
            });
            db.SaveChanges();
        }

        var created = await (await client.PostAsJsonAsync($"/api/v1/projects/{slug}/issues",
            new CreateIssueRequest("Gated issue", "body"))).Content.ReadFromJsonAsync<IssueDto>();
        issueDbId = created!.Id;
        issueSeq = created.Sequence;
        return (client, issueDbId, issueSeq, doneId);
    }

    [Fact]
    public async Task Transition_to_Completed_returns_412_when_AC_unchecked()
    {
        var (client, _, seq, doneId) = await SetupProjectIssueAndDone(_pg, "gate1", "GT1");

        await client.PostAsJsonAsync($"/api/v1/projects/gate1/issues/{seq}/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("must pass tests"));
        await client.PostAsJsonAsync($"/api/v1/projects/gate1/issues/{seq}/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("must update docs"));

        var resp = await client.PostAsJsonAsync($"/api/v1/projects/gate1/issues/{seq}/transitions",
            new TransitionIssueRequest(doneId));
        ((int)resp.StatusCode).Should().Be(412);
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Error.Code.Should().Be("acceptance_criteria_unchecked");
    }

    [Fact]
    public async Task Transition_to_Completed_succeeds_when_all_AC_checked()
    {
        var (client, _, seq, doneId) = await SetupProjectIssueAndDone(_pg, "gate2", "GT2");

        var ac1 = await (await client.PostAsJsonAsync(
            $"/api/v1/projects/gate2/issues/{seq}/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("one"))).Content.ReadFromJsonAsync<AcceptanceCriterionDto>();
        var ac2 = await (await client.PostAsJsonAsync(
            $"/api/v1/projects/gate2/issues/{seq}/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("two"))).Content.ReadFromJsonAsync<AcceptanceCriterionDto>();
        await client.PostAsync($"/api/v1/projects/gate2/issues/{seq}/acceptance-criteria/{ac1!.Id}/check", content: null);
        await client.PostAsync($"/api/v1/projects/gate2/issues/{seq}/acceptance-criteria/{ac2!.Id}/check", content: null);

        var resp = await client.PostAsJsonAsync($"/api/v1/projects/gate2/issues/{seq}/transitions",
            new TransitionIssueRequest(doneId));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await resp.Content.ReadFromJsonAsync<IssueDto>();
        updated!.StateName.Should().Be("Done");
    }

    [Fact]
    public async Task Transition_to_Completed_with_force_true_bypasses_gate()
    {
        var (client, _, seq, doneId) = await SetupProjectIssueAndDone(_pg, "gate3", "GT3");

        await client.PostAsJsonAsync($"/api/v1/projects/gate3/issues/{seq}/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("still unchecked"));

        var resp = await client.PostAsJsonAsync($"/api/v1/projects/gate3/issues/{seq}/transitions",
            new TransitionIssueRequest(doneId, Force: true, BypassReason: "manual override for test"));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await resp.Content.ReadFromJsonAsync<IssueDto>();
        updated!.StateName.Should().Be("Done");
    }

    [Fact]
    public async Task Transition_to_Completed_with_no_AC_at_all_succeeds()
    {
        var (client, _, seq, doneId) = await SetupProjectIssueAndDone(_pg, "gate4", "GT4");

        // No AC items added. Gate has nothing to refuse on.
        var resp = await client.PostAsJsonAsync($"/api/v1/projects/gate4/issues/{seq}/transitions",
            new TransitionIssueRequest(doneId));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
