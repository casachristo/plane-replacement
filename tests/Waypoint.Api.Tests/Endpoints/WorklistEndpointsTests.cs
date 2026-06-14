using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Waypoint.Api.Auth;
using Waypoint.Api.Repositories;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Waypoint.Domain;
using Waypoint.Domain.Entities;
using Waypoint.Domain.Enums;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

/// <summary>
/// WAY-17/18: the internal-only batch Worklist. Cairn's dispatcher drives start → advance/skip
/// → drain; WAY-18 fires worklist.current_advanced on every pointer change.
/// </summary>
public class WorklistEndpointsTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public WorklistEndpointsTests(PostgresFixture pg) => _pg = pg;

    private static Principal Service() => new(
        PrincipalKind.InternalService, "77777777-7777-7777-7777-777777777777",
        "cairn-dispatcher", ["issue:read", "issue:create", "admin"], TokenKind: TokenKind.Service);

    private async Task<(WaypointApiFactory factory, HttpClient client)> SetupWithIssues(
        string slug, string ident, params (string title, Priority prio)[] issues)
    {
        var factory = new WaypointApiFactory
        {
            PostgresConnectionString = _pg.ConnectionString,
            TestPrincipal = Service(),
        };
        await factory.EnsureMigratedAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var projects = scope.ServiceProvider.GetRequiredService<Waypoint.Api.Subsystems.Projects.IProjectsOrchestrator>();
            var project = await projects.ProvisionAsync(new CreateProjectRequest(slug, "P", ident), CancellationToken.None);
            var db = scope.ServiceProvider.GetRequiredService<WaypointDbContext>();
            var todo = db.States.Single(s => s.ProjectId == project.Id && s.Name == "To Do");
            var type = db.IssueTypes.Single(t => t.ProjectId == project.Id);
            var seq = 1;
            foreach (var (title, prio) in issues)
                db.Issues.Add(new Issue
                {
                    ProjectId = project.Id, SequenceId = seq++, Title = title,
                    StateId = todo.Id, IssueTypeId = type.Id, Priority = prio,
                });
            db.SaveChanges();
        }
        return (factory, factory.CreateClient());
    }

    private static string Base(string slug) => $"/internal/v1/projects/{slug}/worklist";

    [Fact]
    public async Task Worklist_is_auto_created_inactive_with_a_new_project()
    {
        var (factory, client) = await SetupWithIssues("wlq0", "WLQ0");
        await using (factory)
        {
            var status = await (await client.GetAsync(Base("wlq0"))).Content.ReadFromJsonAsync<WorklistStatusDto>();
            status!.State.Should().Be("inactive");
            status.Current.Should().BeNull();
            status.DoneCount.Should().Be(0);
        }
    }

    [Fact]
    public async Task Start_orders_by_priority_desc_then_sequence_asc()
    {
        var (factory, client) = await SetupWithIssues("wlq1", "WLQ1",
            ("low-1", Priority.Low), ("urgent-2", Priority.Urgent), ("low-3", Priority.Low), ("high-4", Priority.High));
        await using (factory)
        {
            var status = await (await client.PostAsync($"{Base("wlq1")}/start", null))
                .Content.ReadFromJsonAsync<WorklistStatusDto>();
            status!.State.Should().Be("active");
            status.Current!.Title.Should().Be("urgent-2");        // highest priority first
            status.RemainingCount.Should().Be(4);
            status.DoneCount.Should().Be(0);
        }
    }

    [Fact]
    public async Task Start_while_active_returns_409()
    {
        var (factory, client) = await SetupWithIssues("wlq2", "WLQ2", ("a", Priority.Medium));
        await using (factory)
        {
            (await client.PostAsync($"{Base("wlq2")}/start", null)).EnsureSuccessStatusCode();
            var resp = await client.PostAsync($"{Base("wlq2")}/start", null);
            ((int)resp.StatusCode).Should().Be(409);
            (await resp.Content.ReadFromJsonAsync<ErrorResponse>())!.Error.Code.Should().Be("worklist_active");
        }
    }

    [Fact]
    public async Task Advance_through_all_items_drains_to_inactive()
    {
        var (factory, client) = await SetupWithIssues("wlq3", "WLQ3",
            ("a", Priority.High), ("b", Priority.Medium), ("c", Priority.Low));
        await using (factory)
        {
            await client.PostAsync($"{Base("wlq3")}/start", null);
            await client.PostAsync($"{Base("wlq3")}/advance", null);   // a -> b
            await client.PostAsync($"{Base("wlq3")}/advance", null);   // b -> c
            var drained = await (await client.PostAsync($"{Base("wlq3")}/advance", null))
                .Content.ReadFromJsonAsync<WorklistStatusDto>();        // c -> drained

            drained!.State.Should().Be("inactive");
            drained.Current.Should().BeNull();
            drained.DoneCount.Should().Be(3);
            drained.RemainingCount.Should().Be(0);
            drained.CompletedAt.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task Advance_on_drained_worklist_is_idempotent_noop()
    {
        var (factory, client) = await SetupWithIssues("wlq4", "WLQ4", ("only", Priority.Medium));
        await using (factory)
        {
            await client.PostAsync($"{Base("wlq4")}/start", null);
            await client.PostAsync($"{Base("wlq4")}/advance", null);   // drains (1 item)
            var again = await (await client.PostAsync($"{Base("wlq4")}/advance", null))
                .Content.ReadFromJsonAsync<WorklistStatusDto>();
            again!.State.Should().Be("inactive");
            again.DoneCount.Should().Be(1);   // unchanged by the no-op advance
        }
    }

    [Fact]
    public async Task Skip_requires_a_non_empty_reason()
    {
        var (factory, client) = await SetupWithIssues("wlq5", "WLQ5", ("a", Priority.Medium));
        await using (factory)
        {
            await client.PostAsync($"{Base("wlq5")}/start", null);
            var resp = await client.PostAsJsonAsync($"{Base("wlq5")}/skip", new SkipWorklistRequest("  "));
            ((int)resp.StatusCode).Should().Be(422);
            (await resp.Content.ReadFromJsonAsync<ErrorResponse>())!.Error.Code.Should().Be("reason_required");
        }
    }

    [Fact]
    public async Task Skip_files_a_comment_and_advances_past_the_rest()
    {
        var (factory, client) = await SetupWithIssues("wlq6", "WLQ6",
            ("a", Priority.High), ("b", Priority.Low));
        await using (factory)
        {
            await client.PostAsync($"{Base("wlq6")}/start", null);     // current = a
            var afterSkip = await (await client.PostAsJsonAsync($"{Base("wlq6")}/skip",
                new SkipWorklistRequest("blocked on infra"))).Content.ReadFromJsonAsync<WorklistStatusDto>();
            afterSkip!.Current!.Title.Should().Be("b");                // advanced to next
            afterSkip.SkippedCount.Should().Be(1);
            afterSkip.DoneCount.Should().Be(0);                        // skip is not "done"

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WaypointDbContext>();
            var comment = await db.Comments.SingleAsync(c => c.Issue.Title == "a");
            comment.BodyMd.Should().Be("Skipped during batch run: blocked on infra");
        }
    }

    [Fact]
    public async Task Start_after_stop_rebuilds_from_latest_backlog()
    {
        var (factory, client) = await SetupWithIssues("wlq7", "WLQ7", ("a", Priority.Medium));
        await using (factory)
        {
            await client.PostAsync($"{Base("wlq7")}/start", null);
            var stop = await (await client.PostAsync($"{Base("wlq7")}/stop", null))
                .Content.ReadFromJsonAsync<WorklistStopSummary>();
            stop!.RemainingCount.Should().Be(1);

            // Add a second issue, then restart — the queue rebuilds to include it.
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<WaypointDbContext>();
                var p = db.Projects.Single(x => x.Slug == "wlq7");
                var todo = db.States.Single(s => s.ProjectId == p.Id && s.Name == "To Do");
                var type = db.IssueTypes.Single(t => t.ProjectId == p.Id);
                db.Issues.Add(new Issue { ProjectId = p.Id, SequenceId = 2, Title = "b", StateId = todo.Id, IssueTypeId = type.Id });
                db.SaveChanges();
            }
            var restarted = await (await client.PostAsync($"{Base("wlq7")}/start", null))
                .Content.ReadFromJsonAsync<WorklistStatusDto>();
            restarted!.RemainingCount.Should().Be(2);
        }
    }

    [Fact]
    public async Task Worklist_is_not_exposed_on_the_public_surface()
    {
        var (factory, client) = await SetupWithIssues("wlq8", "WLQ8", ("a", Priority.Medium));
        await using (factory)
        {
            var resp = await client.PostAsync("/api/v1/projects/wlq8/worklist/start", null);
            ((int)resp.StatusCode).Should().Be(404);
        }
    }

    // WAY-18: every pointer change fires worklist.current_advanced.
    [Fact]
    public async Task Run_fires_one_advanced_delivery_per_pointer_change()
    {
        var (factory, client) = await SetupWithIssues("wlq9", "WLQ9",
            ("a", Priority.High), ("b", Priority.Medium), ("c", Priority.Low));
        await using (factory)
        {
            Guid subId;
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<WaypointDbContext>();
                var sub = new WebhookSubscription
                {
                    ProjectId = null,
                    TargetUrl = "http://example.invalid/hook",
                    EventMask = (long)WebhookEvent.WorklistCurrentAdvanced,
                    Secret = "s",
                };
                db.WebhookSubscriptions.Add(sub);
                db.SaveChanges();
                subId = sub.Id;
            }

            await client.PostAsync($"{Base("wlq9")}/start", null);     // null -> a   (advance)
            await client.PostAsync($"{Base("wlq9")}/advance", null);   // a -> b      (advance)
            await client.PostAsync($"{Base("wlq9")}/advance", null);   // b -> c      (advance)
            await client.PostAsync($"{Base("wlq9")}/advance", null);   // c -> drained(drained)

            using var verify = factory.Services.CreateScope();
            var vdb = verify.ServiceProvider.GetRequiredService<WaypointDbContext>();
            var deliveries = await vdb.WebhookDeliveries.AsNoTracking()
                .Where(d => d.SubscriptionId == subId).OrderBy(d => d.CreatedAt).ToListAsync();

            deliveries.Should().HaveCount(4);
            deliveries.Should().OnlyContain(d => d.Event == "worklist.current_advanced");
            var triggers = deliveries
                .Select(d => JsonDocument.Parse(d.PayloadJson).RootElement
                    .GetProperty("payload").GetProperty("trigger").GetString())
                .ToList();
            triggers.Should().Equal("advance", "advance", "advance", "drained");

            var last = JsonDocument.Parse(deliveries[3].PayloadJson).RootElement.GetProperty("payload");
            // The drained delivery has no current — null fields are omitted by the serializer.
            last.TryGetProperty("new_current", out _).Should().BeFalse();
            last.GetProperty("state").GetString().Should().Be("inactive");
            last.GetProperty("done_count").GetInt32().Should().Be(3);
        }
    }
}
