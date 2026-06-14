using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Waypoint.Api.Auth;
using Waypoint.Api.Repositories;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Waypoint.Domain;
using Waypoint.Domain.Entities;
using Waypoint.Domain.Enums;
using Xunit;

namespace Waypoint.Api.Tests.Auth;

/// <summary>
/// WAY-19: a WRITER credential can create/edit issues but may NOT transition state (403);
/// only a credential with issue:transition (or admin) can. This is the structural half of
/// Cairn's transition gate — agents hold writer tokens and route state changes through Cairn.
/// </summary>
public class TransitionRightsTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public TransitionRightsTests(PostgresFixture pg) => _pg = pg;

    private static Principal Writer() => new(
        PrincipalKind.InternalService, "55555555-5555-5555-5555-555555555555",
        "writer-token", ["issue:read", "issue:create"], TokenKind: TokenKind.Service);

    private static Principal Transitioner() => new(
        PrincipalKind.InternalService, "66666666-6666-6666-6666-666666666666",
        "ci-token", ["issue:read", "issue:transition"], TokenKind: TokenKind.Service);

    private async Task<(WaypointApiFactory factory, int seq, Guid doneId)> SetupAs(
        Principal principal, string slug, string ident)
    {
        var factory = new WaypointApiFactory
        {
            PostgresConnectionString = _pg.ConnectionString,
            TestPrincipal = principal,
        };
        await factory.EnsureMigratedAsync();

        Guid doneId;
        int seq;
        using (var scope = factory.Services.CreateScope())
        {
            // Provisioning is admin/out-of-band (WAY-5) — seed via the repository.
            var repo = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
            var project = await repo.CreateAsync(slug, "P", ident, CancellationToken.None);
            var db = scope.ServiceProvider.GetRequiredService<WaypointDbContext>();
            var done = db.States.Single(s => s.ProjectId == project.Id && s.Name == "Done");
            doneId = done.Id;
            var workflow = db.Workflows.Single(w => w.ProjectId == project.Id);
            db.WorkflowTransitions.Add(new WorkflowTransition
            {
                WorkflowId = workflow.Id,
                FromStateId = project.DefaultStateId!.Value,
                ToStateId = done.Id,
            });
            await db.SaveChangesAsync();

            // Seed the issue out-of-band too: the principal under test may lack
            // issue:create (WAY-27), and provisioning is admin/out-of-band anyway.
            var issues = scope.ServiceProvider.GetRequiredService<IIssueRepository>();
            var created = await issues.CreateAsync(project.Id, "X", "y", null, null, null, Waypoint.Domain.Enums.TicketCategory.Feature, CancellationToken.None);
            seq = created.SequenceId;
        }
        return (factory, seq, doneId);
    }

    [Fact]
    public async Task Writer_token_can_create_an_issue_but_cannot_transition_it()
    {
        var (factory, seq, doneId) = await SetupAs(Writer(), "wr1", "WR1");
        await using (factory)
        {
            using var client = factory.CreateClient();
            var resp = await client.PostAsJsonAsync($"/api/v1/projects/wr1/issues/{seq}/transitions",
                new TransitionIssueRequest(doneId));
            ((int)resp.StatusCode).Should().Be(403);
            var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
            err!.Error.Code.Should().Be("transition_forbidden");
        }
    }

    [Fact]
    public async Task Token_with_issue_transition_scope_can_transition()
    {
        var (factory, seq, doneId) = await SetupAs(Transitioner(), "wr2", "WR2");
        await using (factory)
        {
            using var client = factory.CreateClient();
            var resp = await client.PostAsJsonAsync($"/api/v1/projects/wr2/issues/{seq}/transitions",
                new TransitionIssueRequest(doneId));
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
            var updated = await resp.Content.ReadFromJsonAsync<IssueDto>();
            updated!.StateName.Should().Be("Done");
        }
    }
}
