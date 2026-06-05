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
            .GetProperty("name").GetString().Should().Be("Backlog");
        env.GetProperty("payload").GetProperty("state")
            .GetProperty("group").GetString().Should().Be("Backlog");
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
}
