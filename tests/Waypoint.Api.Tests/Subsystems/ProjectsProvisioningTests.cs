using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Waypoint.Api.Subsystems.Projects;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Waypoint.Domain;
using Waypoint.Domain.Entities;
using Waypoint.Domain.Enums;
using Xunit;

namespace Waypoint.Api.Tests.Subsystems;

// Integration test for the Projects subsystem Orchestrator: one ProvisionAsync call must seed
// the full project skeleton — states (covered by ProjectEndpointsTests), plus the Default
// workflow, the default "Task" issue type bound to it, and the dormant batch worklist.
public class ProjectsProvisioningTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public ProjectsProvisioningTests(PostgresFixture pg) => _pg = pg;

    [Fact]
    public async Task ProvisionAsync_seeds_default_state_workflow_issue_type_and_dormant_worklist()
    {
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();

        using var scope = factory.Services.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IProjectsOrchestrator>();
        var dto = await orchestrator.ProvisionAsync(new CreateProjectRequest("prov-proj", "Prov", "PRV"), CancellationToken.None);

        var db = scope.ServiceProvider.GetRequiredService<WaypointDbContext>();

        // Default landing state points at "To Do".
        var project = await db.Projects.AsNoTracking().SingleAsync(p => p.Id == dto.Id);
        var todo = await db.States.AsNoTracking().SingleAsync(s => s.ProjectId == dto.Id && s.Name == "To Do");
        project.DefaultStateId.Should().Be(todo.Id);

        // Exactly one "Default" workflow.
        var workflows = await db.Workflows.AsNoTracking().Where(w => w.ProjectId == dto.Id).ToListAsync();
        workflows.Select(w => w.Name).Should().Equal("Default");

        // Exactly one default "Task" issue type, bound to that workflow.
        var types = await db.IssueTypes.AsNoTracking().Where(t => t.ProjectId == dto.Id).ToListAsync();
        types.Should().ContainSingle();
        types[0].Name.Should().Be("Task");
        types[0].IsDefault.Should().BeTrue();
        types[0].DefaultWorkflowId.Should().Be(workflows[0].Id);

        // Exactly one worklist, dormant.
        var worklists = await db.Set<Worklist>().AsNoTracking().Where(w => w.ProjectId == dto.Id).ToListAsync();
        worklists.Should().ContainSingle();
        worklists[0].State.Should().Be(WorklistState.Inactive);
    }
}
