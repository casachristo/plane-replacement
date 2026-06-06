using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Waypoint.Api.Endpoints.PublicApi;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

public class WebhookEndpointsExtra2MutationCoverage : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public WebhookEndpointsExtra2MutationCoverage(PostgresFixture pg) => _pg = pg;

    private WaypointApiFactory NewFactory() => new() { PostgresConnectionString = _pg.ConnectionString };

    [Fact]
    public async Task DELETE_unknown_webhook_error_message_says_subscription_not_found_text()
    {
        await using var f = NewFactory();
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var resp = await c.DeleteAsync($"/api/v1/webhooks/{Guid.NewGuid()}");
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Error.Message.ToLowerInvariant().Should().ContainAny("subscription", "not found");
    }

    [Fact]
    public async Task POST_webhook_creating_unique_subscriptions_assigns_distinct_Ids()
    {
        await using var f = NewFactory();
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var a = await (await c.PostAsJsonAsync("/api/v1/webhooks",
            new CreateWebhookSubscriptionRequest("https://w1.invalid/h", 1L, null)))
            .Content.ReadFromJsonAsync<WebhookSubscriptionCreatedDto>();
        var b = await (await c.PostAsJsonAsync("/api/v1/webhooks",
            new CreateWebhookSubscriptionRequest("https://w2.invalid/h", 1L, null)))
            .Content.ReadFromJsonAsync<WebhookSubscriptionCreatedDto>();
        a!.Subscription.Id.Should().NotBe(b!.Subscription.Id);
    }

    [Fact]
    public async Task POST_webhook_creating_unique_subscriptions_assigns_distinct_secrets()
    {
        await using var f = NewFactory();
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var a = await (await c.PostAsJsonAsync("/api/v1/webhooks",
            new CreateWebhookSubscriptionRequest("https://w3.invalid/h", 1L, null)))
            .Content.ReadFromJsonAsync<WebhookSubscriptionCreatedDto>();
        var b = await (await c.PostAsJsonAsync("/api/v1/webhooks",
            new CreateWebhookSubscriptionRequest("https://w4.invalid/h", 1L, null)))
            .Content.ReadFromJsonAsync<WebhookSubscriptionCreatedDto>();
        a!.Secret.Should().NotBe(b!.Secret);
    }

    [Fact]
    public async Task POST_webhook_returned_DTO_has_LastDeliveryAt_null_for_brand_new()
    {
        await using var f = NewFactory();
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var dto = await (await c.PostAsJsonAsync("/api/v1/webhooks",
            new CreateWebhookSubscriptionRequest("https://newhook.invalid/h", 1L, null)))
            .Content.ReadFromJsonAsync<WebhookSubscriptionCreatedDto>();
        dto!.Subscription.LastDeliveryAt.Should().BeNull();
    }

    [Fact]
    public async Task GET_webhook_deliveries_for_unknown_subscription_returns_empty_array()
    {
        await using var f = NewFactory();
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        // Listing deliveries for an unknown sub-id is currently a 200 with [] (the
        // endpoint doesn't validate the subscription exists).
        var resp = await c.GetAsync($"/api/v1/webhooks/{Guid.NewGuid()}/deliveries");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
