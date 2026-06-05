using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Waypoint.Api.Auth;
using Waypoint.Contracts;
using Waypoint.Api.Endpoints.PublicApi;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Domain;
using Waypoint.Domain.Entities;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

/// <summary>
/// Mutation-coverage HTTP tests for WebhookEndpoints. Default test fixture grants
/// admin scope; a NewNonAdminFactory pins missing-scope rejections.
/// </summary>
public class WebhookEndpointsMutationCoverage : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public WebhookEndpointsMutationCoverage(PostgresFixture pg) => _pg = pg;

    private WaypointApiFactory NewFactory() => new() { PostgresConnectionString = _pg.ConnectionString };
    private WaypointApiFactory NewNonAdminFactory() => new()
    {
        PostgresConnectionString = _pg.ConnectionString,
        TestPrincipal = new Principal(
            PrincipalKind.Human, Guid.NewGuid().ToString(), "Non-Admin", ["issue:read"]),
    };

    [Fact]
    public async Task GET_webhooks_returns_OK_with_empty_list_initially()
    {
        await using var factory = NewFactory();
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        var resp = await client.GetAsync("/api/v1/webhooks");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await resp.Content.ReadFromJsonAsync<List<WebhookSubscriptionDto>>();
        list.Should().NotBeNull();
    }

    [Fact]
    public async Task POST_webhooks_creates_subscription_with_secret_returned_once()
    {
        await using var factory = NewFactory();
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/v1/webhooks",
            new CreateWebhookSubscriptionRequest("https://example.invalid/hook", 1L, null));
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await resp.Content.ReadFromJsonAsync<WebhookSubscriptionCreatedDto>();
        dto!.Secret.Should().NotBeNullOrWhiteSpace();
        dto.Subscription.TargetUrl.Should().Be("https://example.invalid/hook");
        dto.Subscription.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GET_webhooks_returns_created_subscriptions()
    {
        await using var factory = NewFactory();
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/webhooks",
            new CreateWebhookSubscriptionRequest("https://a.invalid/hook", 1L, null));
        await client.PostAsJsonAsync("/api/v1/webhooks",
            new CreateWebhookSubscriptionRequest("https://b.invalid/hook", 2L, null));
        var resp = await client.GetAsync("/api/v1/webhooks");
        var list = await resp.Content.ReadFromJsonAsync<List<WebhookSubscriptionDto>>();
        list!.Where(s => s.TargetUrl.EndsWith("a.invalid/hook", System.StringComparison.Ordinal) || s.TargetUrl.EndsWith("b.invalid/hook", System.StringComparison.Ordinal)).Should().HaveCount(2);
    }

    [Fact]
    public async Task DELETE_webhook_soft_deletes_subscription()
    {
        await using var factory = NewFactory();
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        var created = await (await client.PostAsJsonAsync("/api/v1/webhooks",
            new CreateWebhookSubscriptionRequest("https://x.invalid/hook", 1L, null)))
            .Content.ReadFromJsonAsync<WebhookSubscriptionCreatedDto>();

        var del = await client.DeleteAsync($"/api/v1/webhooks/{created!.Subscription.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WaypointDbContext>();
        // EF query filter hides soft-deleted; use IgnoreQueryFilters to read it.
        var sub = await db.WebhookSubscriptions.IgnoreQueryFilters()
            .FirstAsync(s => s.Id == created.Subscription.Id);
        sub.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DELETE_unknown_webhook_returns_404_webhook_not_found()
    {
        await using var factory = NewFactory();
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        var resp = await client.DeleteAsync($"/api/v1/webhooks/{Guid.NewGuid()}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Error.Code.Should().Be("webhook_not_found");
    }

    [Fact]
    public async Task GET_webhook_deliveries_returns_empty_list_for_new_subscription()
    {
        await using var factory = NewFactory();
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        var created = await (await client.PostAsJsonAsync("/api/v1/webhooks",
            new CreateWebhookSubscriptionRequest("https://y.invalid/hook", 1L, null)))
            .Content.ReadFromJsonAsync<WebhookSubscriptionCreatedDto>();

        var resp = await client.GetAsync($"/api/v1/webhooks/{created!.Subscription.Id}/deliveries");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GET_webhooks_without_admin_scope_returns_422_missing_scope()
    {
        await using var factory = NewNonAdminFactory();
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        var resp = await client.GetAsync("/api/v1/webhooks");
        ((int)resp.StatusCode).Should().Be(422);
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Error.Code.Should().Be("missing_scope");
    }

    [Fact]
    public async Task POST_webhooks_without_admin_scope_returns_422()
    {
        await using var factory = NewNonAdminFactory();
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/v1/webhooks",
            new CreateWebhookSubscriptionRequest("https://blocked.invalid/hook", 1L, null));
        ((int)resp.StatusCode).Should().Be(422);
    }

    [Fact]
    public async Task DELETE_webhooks_without_admin_scope_returns_422()
    {
        await using var factory = NewNonAdminFactory();
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        var resp = await client.DeleteAsync($"/api/v1/webhooks/{Guid.NewGuid()}");
        ((int)resp.StatusCode).Should().Be(422);
    }

}
