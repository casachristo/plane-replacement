using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Waypoint.Api.Auth;
using Waypoint.Api.Endpoints.PublicApi;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Waypoint.Domain;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

/// <summary>
/// Additional mutation-coverage tests for WebhookEndpoints — pins specific
/// response-DTO fields (Id, EventMask, IsActive, CreatedAt, ProjectId=null when
/// workspace-wide) and the secret-generation contract.
/// </summary>
public class WebhookEndpointsExtraMutationCoverage : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public WebhookEndpointsExtraMutationCoverage(PostgresFixture pg) => _pg = pg;

    private WaypointApiFactory NewFactory() => new() { PostgresConnectionString = _pg.ConnectionString };

    [Fact]
    public async Task POST_webhook_returned_secret_is_at_least_32_bytes_base64()
    {
        await using var factory = NewFactory();
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/v1/webhooks",
            new CreateWebhookSubscriptionRequest("https://x.invalid/hook", 1L, null));
        var dto = await resp.Content.ReadFromJsonAsync<WebhookSubscriptionCreatedDto>();
        var bytes = Convert.FromBase64String(dto!.Secret);
        bytes.Length.Should().BeGreaterThanOrEqualTo(32);
    }

    [Fact]
    public async Task POST_webhook_returned_subscription_has_correct_EventMask()
    {
        await using var factory = NewFactory();
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/v1/webhooks",
            new CreateWebhookSubscriptionRequest("https://x.invalid/hook", 0b101010L, null));
        var dto = await resp.Content.ReadFromJsonAsync<WebhookSubscriptionCreatedDto>();
        dto!.Subscription.EventMask.Should().Be(0b101010L);
    }

    [Fact]
    public async Task POST_webhook_returned_subscription_has_IsActive_true()
    {
        await using var factory = NewFactory();
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/v1/webhooks",
            new CreateWebhookSubscriptionRequest("https://x.invalid/hook", 1L, null));
        var dto = await resp.Content.ReadFromJsonAsync<WebhookSubscriptionCreatedDto>();
        dto!.Subscription.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task POST_webhook_returned_subscription_has_null_ProjectId_when_workspace_wide()
    {
        await using var factory = NewFactory();
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/v1/webhooks",
            new CreateWebhookSubscriptionRequest("https://x.invalid/hook", 1L, null));
        var dto = await resp.Content.ReadFromJsonAsync<WebhookSubscriptionCreatedDto>();
        dto!.Subscription.ProjectId.Should().BeNull();
    }

    [Fact]
    public async Task GET_webhooks_orders_subscriptions_by_CreatedAt_descending()
    {
        await using var factory = NewFactory();
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/webhooks",
            new CreateWebhookSubscriptionRequest("https://order-a.invalid/hook", 1L, null));
        // Tiny delay so CreatedAt timestamps differ.
        await Task.Delay(20);
        await client.PostAsJsonAsync("/api/v1/webhooks",
            new CreateWebhookSubscriptionRequest("https://order-b.invalid/hook", 1L, null));

        var resp = await client.GetAsync("/api/v1/webhooks");
        var list = await resp.Content.ReadFromJsonAsync<List<WebhookSubscriptionDto>>();
        var idxA = list!.FindIndex(s => s.TargetUrl == "https://order-a.invalid/hook");
        var idxB = list.FindIndex(s => s.TargetUrl == "https://order-b.invalid/hook");
        idxB.Should().BeLessThan(idxA);   // newer (b) appears first in DESC order
    }

    [Fact]
    public async Task GET_webhooks_does_not_return_soft_deleted_subscriptions()
    {
        await using var factory = NewFactory();
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        var created = await (await client.PostAsJsonAsync("/api/v1/webhooks",
            new CreateWebhookSubscriptionRequest("https://soft.invalid/hook", 1L, null)))
            .Content.ReadFromJsonAsync<WebhookSubscriptionCreatedDto>();
        await client.DeleteAsync($"/api/v1/webhooks/{created!.Subscription.Id}");

        var resp = await client.GetAsync("/api/v1/webhooks");
        var list = await resp.Content.ReadFromJsonAsync<List<WebhookSubscriptionDto>>();
        list!.Any(s => s.Id == created.Subscription.Id).Should().BeFalse();
    }
}
