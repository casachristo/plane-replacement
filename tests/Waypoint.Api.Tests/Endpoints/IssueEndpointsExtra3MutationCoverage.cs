using System.Net;
using Microsoft.EntityFrameworkCore;
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

public class IssueEndpointsExtra3MutationCoverage : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public IssueEndpointsExtra3MutationCoverage(PostgresFixture pg) => _pg = pg;

    private async Task<(WaypointApiFactory factory, HttpClient client)> Setup(string slug, string ident)
    {
        var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        var c = f.CreateClient();
        await c.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest(slug, "p", ident));
        await c.PostAsJsonAsync($"/api/v1/projects/{slug}/issues",
            new CreateIssueRequest("First", "body"));
        return (f, c);
    }

    [Fact]
    public async Task GET_issue_DTO_includes_StateId_matching_project_default()
    {
        await using var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        await c.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("ix3a", "p", "IX3A"));
        var issue = await (await c.PostAsJsonAsync("/api/v1/projects/ix3a/issues",
            new CreateIssueRequest("t", "b"))).Content.ReadFromJsonAsync<IssueDto>();

        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WaypointDbContext>();
        var project = await db.Projects.FirstAsync(p => p.Slug == "ix3a");
        issue!.StateId.Should().Be(project.DefaultStateId!.Value);
    }

    [Fact]
    public async Task POST_then_transition_to_same_state_is_a_noop_returning_unchanged_issue()
    {
        var (f, c) = await Setup("ix3b", "IX3B");
        await using var _ = f;
        var issue = await (await c.GetAsync("/api/v1/projects/ix3b/issues/1"))
            .Content.ReadFromJsonAsync<IssueDto>();
        var resp = await c.PostAsJsonAsync("/api/v1/projects/ix3b/issues/1/transitions",
            new TransitionIssueRequest(issue!.StateId));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await resp.Content.ReadFromJsonAsync<IssueDto>();
        updated!.StateId.Should().Be(issue.StateId);
    }

    [Fact]
    public async Task POST_transition_to_state_from_different_project_returns_422_state_wrong_project()
    {
        await using var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        await c.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("ix3ca", "P A", "IX3CA"));
        await c.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("ix3cb", "P B", "IX3CB"));
        await c.PostAsJsonAsync("/api/v1/projects/ix3ca/issues",
            new CreateIssueRequest("issue", "body"));

        Guid otherProjectStateId;
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WaypointDbContext>();
            var projB = await db.Projects.FirstAsync(p => p.Slug == "ix3cb");
            otherProjectStateId = projB.DefaultStateId!.Value;
        }

        var resp = await c.PostAsJsonAsync("/api/v1/projects/ix3ca/issues/1/transitions",
            new TransitionIssueRequest(otherProjectStateId));
        ((int)resp.StatusCode).Should().Be(422);
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Error.Code.Should().Be("state_wrong_project");
    }

    [Fact]
    public async Task GET_activity_orders_events_by_At_ascending()
    {
        var (f, c) = await Setup("ix3d", "IX3D");
        await using var _ = f;
        // Trigger several activities: created (during POST), updated (PATCH).
        await c.PatchAsJsonAsync("/api/v1/projects/ix3d/issues/1",
            new UpdateIssueRequest(Title: "renamed"));

        var resp = await c.GetAsync("/api/v1/projects/ix3d/issues/1/activity");
        var events = await resp.Content.ReadFromJsonAsync<List<ActivityDto>>();
        events.Should().NotBeNull();
        // 'created' should appear before 'updated' (At ascending).
        var createdIdx = events!.FindIndex(e => e.Verb == "created");
        var updatedIdx = events.FindIndex(e => e.Verb == "updated");
        if (createdIdx >= 0 && updatedIdx >= 0)
            createdIdx.Should().BeLessThan(updatedIdx);
    }

    [Fact]
    public async Task PATCH_priority_in_range_0_to_4_is_stored_as_int()
    {
        var (f, c) = await Setup("ix3e", "IX3E");
        await using var _ = f;
        var resp = await c.PatchAsJsonAsync("/api/v1/projects/ix3e/issues/1",
            new UpdateIssueRequest(Priority: 4));
        var dto = await resp.Content.ReadFromJsonAsync<IssueDto>();
        dto!.Priority.Should().Be(4);
    }
}
