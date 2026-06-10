using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Waypoint.Domain;
using Waypoint.Domain.Entities;
using Waypoint.Domain.Enums;
using Xunit;

namespace Waypoint.Api.Tests.Webhooks;

/// <summary>
/// WAY-6 / WAY-13: any subscription matching an event class gets a WebhookDelivery
/// row queued with the canonical payload (state_id + state_name + state_group
/// together, every payload self-describing).
/// </summary>
public class WebhookEventTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public WebhookEventTests(PostgresFixture pg) => _pg = pg;

    private static async Task<(WaypointApiFactory factory, HttpClient client, Guid subId)> SetupSubscription(
        PostgresFixture pg, string slug, string ident, WebhookEvent eventMask)
    {
        var factory = new WaypointApiFactory { PostgresConnectionString = pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/v1/projects",
            new CreateProjectRequest(slug, $"P {slug}", ident));

        Guid subId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WaypointDbContext>();
            var sub = new WebhookSubscription
            {
                ProjectId = null,    // workspace-wide
                TargetUrl = "http://example.invalid/hook",
                EventMask = (long)eventMask,
                Secret = "test-secret",
            };
            db.WebhookSubscriptions.Add(sub);
            db.SaveChanges();
            subId = sub.Id;
        }
        return (factory, client, subId);
    }

    private static async Task<List<WebhookDelivery>> ReadDeliveries(WaypointApiFactory factory, Guid subId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WaypointDbContext>();
        return await db.WebhookDeliveries.AsNoTracking()
            .Where(d => d.SubscriptionId == subId)
            .OrderBy(d => d.CreatedAt)
            .ToListAsync();
    }

    [Fact]
    public async Task Issue_create_queues_issue_created_delivery()
    {
        var (factory, client, subId) = await SetupSubscription(
            _pg, "hook1", "HK1", WebhookEvent.IssueCreated);

        await client.PostAsJsonAsync("/api/v1/projects/hook1/issues",
            new CreateIssueRequest("X", "y"));

        var deliveries = await ReadDeliveries(factory, subId);
        deliveries.Should().ContainSingle();
        deliveries[0].Event.Should().Be("issue.created");
        var env = JsonDocument.Parse(deliveries[0].PayloadJson).RootElement;
        env.GetProperty("event").GetString().Should().Be("issue.created");
        env.GetProperty("payload").GetProperty("state")
            .GetProperty("name").GetString().Should().Be("To Do");
        env.GetProperty("payload").GetProperty("state")
            .GetProperty("group").GetString().Should().Be("Unstarted");
    }

    [Fact]
    public async Task AC_create_then_check_queues_two_deliveries()
    {
        var (factory, client, subId) = await SetupSubscription(
            _pg, "hook2", "HK2",
            WebhookEvent.AcceptanceCriterionCreated | WebhookEvent.AcceptanceCriterionChecked);

        await client.PostAsJsonAsync("/api/v1/projects/hook2/issues", new CreateIssueRequest("X", "y"));
        var ac = await (await client.PostAsJsonAsync("/api/v1/projects/hook2/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("test"))).Content.ReadFromJsonAsync<AcceptanceCriterionDto>();
        await client.PostAsync($"/api/v1/projects/hook2/issues/1/acceptance-criteria/{ac!.Id}/check", content: null);

        var deliveries = await ReadDeliveries(factory, subId);
        deliveries.Select(d => d.Event).Should().Equal(
            "issue.acceptance_criterion.created",
            "issue.acceptance_criterion.checked");
    }

    [Fact]
    public async Task Subscription_with_no_matching_mask_gets_no_delivery()
    {
        // Subscribe only to comment.created — issue activity should NOT enqueue.
        var (factory, client, subId) = await SetupSubscription(
            _pg, "hook3", "HK3", WebhookEvent.CommentCreated);

        await client.PostAsJsonAsync("/api/v1/projects/hook3/issues", new CreateIssueRequest("X", "y"));

        var deliveries = await ReadDeliveries(factory, subId);
        deliveries.Should().BeEmpty();
    }

    // WAY-13: every acceptance-criterion CRUD verb fires its own webhook event.
    // The created/checked pair is covered above; these cover update, uncheck, delete.

    [Fact]
    public async Task AC_update_queues_updated_delivery()
    {
        var (factory, client, subId) = await SetupSubscription(
            _pg, "hook4", "HK4", WebhookEvent.AcceptanceCriterionUpdated);

        await client.PostAsJsonAsync("/api/v1/projects/hook4/issues", new CreateIssueRequest("X", "y"));
        var ac = await (await client.PostAsJsonAsync("/api/v1/projects/hook4/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("orig"))).Content.ReadFromJsonAsync<AcceptanceCriterionDto>();
        await client.PatchAsJsonAsync($"/api/v1/projects/hook4/issues/1/acceptance-criteria/{ac!.Id}",
            new UpdateAcceptanceCriterionRequest("edited"));

        // Subscription mask is Updated only, so the create above does NOT enqueue — exactly one row.
        var deliveries = await ReadDeliveries(factory, subId);
        deliveries.Should().ContainSingle();
        deliveries[0].Event.Should().Be("issue.acceptance_criterion.updated");
        var crit = JsonDocument.Parse(deliveries[0].PayloadJson).RootElement
            .GetProperty("payload").GetProperty("acceptance_criterion");
        crit.GetProperty("id").GetGuid().Should().Be(ac.Id);
        crit.GetProperty("text").GetString().Should().Be("edited");
    }

    [Fact]
    public async Task AC_uncheck_queues_unchecked_delivery()
    {
        var (factory, client, subId) = await SetupSubscription(
            _pg, "hook5", "HK5", WebhookEvent.AcceptanceCriterionUnchecked);

        await client.PostAsJsonAsync("/api/v1/projects/hook5/issues", new CreateIssueRequest("X", "y"));
        var ac = await (await client.PostAsJsonAsync("/api/v1/projects/hook5/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("c"))).Content.ReadFromJsonAsync<AcceptanceCriterionDto>();
        await client.PostAsync($"/api/v1/projects/hook5/issues/1/acceptance-criteria/{ac!.Id}/check", content: null);
        await client.PostAsync($"/api/v1/projects/hook5/issues/1/acceptance-criteria/{ac.Id}/uncheck", content: null);

        // Only the uncheck matches the mask — create and check do not enqueue.
        var deliveries = await ReadDeliveries(factory, subId);
        deliveries.Should().ContainSingle();
        deliveries[0].Event.Should().Be("issue.acceptance_criterion.unchecked");
        var crit = JsonDocument.Parse(deliveries[0].PayloadJson).RootElement
            .GetProperty("payload").GetProperty("acceptance_criterion");
        crit.GetProperty("checked").GetBoolean().Should().BeFalse();
        // The snake_case serializer omits null fields, so cleared actor fields drop out entirely.
        crit.TryGetProperty("checked_by_actor_id", out _).Should().BeFalse();
        crit.TryGetProperty("checked_at", out _).Should().BeFalse();
    }

    [Fact]
    public async Task AC_delete_queues_deleted_delivery()
    {
        var (factory, client, subId) = await SetupSubscription(
            _pg, "hook6", "HK6", WebhookEvent.AcceptanceCriterionDeleted);

        await client.PostAsJsonAsync("/api/v1/projects/hook6/issues", new CreateIssueRequest("X", "y"));
        var ac = await (await client.PostAsJsonAsync("/api/v1/projects/hook6/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("c"))).Content.ReadFromJsonAsync<AcceptanceCriterionDto>();
        await client.DeleteAsync($"/api/v1/projects/hook6/issues/1/acceptance-criteria/{ac!.Id}");

        var deliveries = await ReadDeliveries(factory, subId);
        deliveries.Should().ContainSingle();
        deliveries[0].Event.Should().Be("issue.acceptance_criterion.deleted");
        JsonDocument.Parse(deliveries[0].PayloadJson).RootElement
            .GetProperty("payload").GetProperty("acceptance_criterion")
            .GetProperty("id").GetGuid().Should().Be(ac.Id);
    }

    [Fact]
    public async Task AC_checked_payload_carries_resolved_actor_id_type_and_label()
    {
        // WAY-7 / WAY-13: the .checked payload identifies WHO checked it (type + id + label),
        // matching the actor recorded on the entity — subscribers need no round-trip to resolve it.
        var (factory, client, subId) = await SetupSubscription(
            _pg, "hook7", "HK7", WebhookEvent.AcceptanceCriterionChecked);

        await client.PostAsJsonAsync("/api/v1/projects/hook7/issues", new CreateIssueRequest("X", "y"));
        var ac = await (await client.PostAsJsonAsync("/api/v1/projects/hook7/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("c"))).Content.ReadFromJsonAsync<AcceptanceCriterionDto>();
        var checkedDto = await (await client.PostAsync(
            $"/api/v1/projects/hook7/issues/1/acceptance-criteria/{ac!.Id}/check", content: null))
            .Content.ReadFromJsonAsync<AcceptanceCriterionDto>();

        var deliveries = await ReadDeliveries(factory, subId);
        deliveries.Should().ContainSingle();
        var crit = JsonDocument.Parse(deliveries[0].PayloadJson).RootElement
            .GetProperty("payload").GetProperty("acceptance_criterion");
        crit.GetProperty("checked").GetBoolean().Should().BeTrue();
        crit.GetProperty("checked_by_actor_type").GetString().Should().Be("User");
        crit.GetProperty("checked_by_actor_id").GetGuid().Should().Be(checkedDto!.CheckedByActorId!.Value);
        crit.GetProperty("checked_by_actor_label").GetString().Should().Be("Test User");
    }

    // WAY-6: the issue.transitioned payload carries BOTH previous and new state, each
    // self-describing (id + name + group), so a subscriber renders the move with no callback.
    [Fact]
    public async Task Issue_transition_queues_transitioned_delivery_with_both_states()
    {
        var (factory, client, subId) = await SetupSubscription(
            _pg, "hook8", "HK8", WebhookEvent.IssueTransitioned);
        var doneId = await AddDoneTransition(factory, "hook8");

        await client.PostAsJsonAsync("/api/v1/projects/hook8/issues", new CreateIssueRequest("X", "y"));
        var resp = await client.PostAsJsonAsync("/api/v1/projects/hook8/issues/1/transitions",
            new TransitionIssueRequest(doneId));
        resp.EnsureSuccessStatusCode();

        var deliveries = await ReadDeliveries(factory, subId);
        deliveries.Should().ContainSingle();
        deliveries[0].Event.Should().Be("issue.transitioned");
        var root = JsonDocument.Parse(deliveries[0].PayloadJson).RootElement;
        root.GetProperty("version").GetInt32().Should().Be(1);   // WAY-6: envelope is versioned
        var payload = root.GetProperty("payload");

        var prev = payload.GetProperty("previous_state");
        prev.GetProperty("name").GetString().Should().Be("To Do");
        prev.GetProperty("group").GetString().Should().Be("Unstarted");
        prev.TryGetProperty("id", out var prevId).Should().BeTrue();
        prevId.GetGuid().Should().NotBeEmpty();

        var next = payload.GetProperty("new_state");
        next.GetProperty("id").GetGuid().Should().Be(doneId);
        next.GetProperty("name").GetString().Should().Be("Done");
        next.GetProperty("group").GetString().Should().Be("Completed");
    }

    // WAY-9: bypassing the AC gate with force=true fires gate.override_fired so reviewers
    // see overrides in real time, with the gate name and the (required) reason in the payload.
    [Fact]
    public async Task Force_bypass_queues_gate_override_fired_delivery()
    {
        var (factory, client, subId) = await SetupSubscription(
            _pg, "hook9", "HK9", WebhookEvent.GateOverrideFired);
        var doneId = await AddDoneTransition(factory, "hook9");

        await client.PostAsJsonAsync("/api/v1/projects/hook9/issues", new CreateIssueRequest("X", "y"));
        await client.PostAsJsonAsync("/api/v1/projects/hook9/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("still unchecked"));

        var resp = await client.PostAsJsonAsync("/api/v1/projects/hook9/issues/1/transitions",
            new TransitionIssueRequest(doneId, Force: true, BypassReason: "shipping; follow-up filed"));
        resp.EnsureSuccessStatusCode();

        var deliveries = await ReadDeliveries(factory, subId);
        deliveries.Should().ContainSingle();
        deliveries[0].Event.Should().Be("gate.override_fired");
        var payload = JsonDocument.Parse(deliveries[0].PayloadJson).RootElement.GetProperty("payload");
        payload.GetProperty("gate_name").GetString().Should().Be("acceptance_criteria_unchecked");
        payload.GetProperty("reason").GetString().Should().Be("shipping; follow-up filed");
    }

    /// <summary>
    /// Wire a DefaultState -> Done workflow transition into the just-created project and return
    /// the Done state id, so transition/override tests have a legal Completed-group target.
    /// </summary>
    private static async Task<Guid> AddDoneTransition(WaypointApiFactory factory, string slug)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WaypointDbContext>();
        var project = db.Projects.Single(p => p.Slug == slug);
        var done = db.States.Single(s => s.ProjectId == project.Id && s.Name == "Done");
        var workflow = db.Workflows.Single(w => w.ProjectId == project.Id);
        db.WorkflowTransitions.Add(new WorkflowTransition
        {
            WorkflowId = workflow.Id,
            FromStateId = project.DefaultStateId!.Value,
            ToStateId = done.Id,
        });
        await db.SaveChangesAsync();
        return done.Id;
    }
}
