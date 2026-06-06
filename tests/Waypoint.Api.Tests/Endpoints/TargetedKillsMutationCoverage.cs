using System.Net.Http.Json;
using FluentAssertions;
using Waypoint.Api.Auth;
using Waypoint.Api.Endpoints.PublicApi;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Waypoint.Domain;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

public class TargetedKillsMutationCoverage : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public TargetedKillsMutationCoverage(PostgresFixture pg) => _pg = pg;

    [Fact]
    public async Task Admin_GET_tokens_orders_by_CreatedAt_descending()
    {
        await using var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        await c.PostAsJsonAsync("/api/admin/tokens",
            new CreateApiTokenRequest("first", Array.Empty<string>(), "Service"));
        await Task.Delay(20);
        await c.PostAsJsonAsync("/api/admin/tokens",
            new CreateApiTokenRequest("second", Array.Empty<string>(), "Service"));

        var list = await (await c.GetAsync("/api/admin/tokens"))
            .Content.ReadFromJsonAsync<List<ApiTokenDto>>();
        var idxFirst = list!.FindIndex(t => t.Name == "first");
        var idxSecond = list.FindIndex(t => t.Name == "second");
        idxSecond.Should().BeLessThan(idxFirst);
    }

    [Fact]
    public async Task Admin_DELETE_only_revokes_the_specified_token_not_others()
    {
        await using var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var a = await (await c.PostAsJsonAsync("/api/admin/tokens",
            new CreateApiTokenRequest("keep", Array.Empty<string>(), "Service")))
            .Content.ReadFromJsonAsync<ApiTokenCreatedDto>();
        var b = await (await c.PostAsJsonAsync("/api/admin/tokens",
            new CreateApiTokenRequest("delete", Array.Empty<string>(), "Service")))
            .Content.ReadFromJsonAsync<ApiTokenCreatedDto>();

        await c.DeleteAsync($"/api/admin/tokens/{b!.Token.Id}");

        var list = await (await c.GetAsync("/api/admin/tokens"))
            .Content.ReadFromJsonAsync<List<ApiTokenDto>>();
        var aRow = list!.First(t => t.Id == a!.Token.Id);
        aRow.RevokedAt.Should().BeNull();
        var bRow = list!.First(t => t.Id == b.Token.Id);
        bRow.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Webhook_GET_orders_subscriptions_by_CreatedAt_descending()
    {
        await using var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        await c.PostAsJsonAsync("/api/v1/webhooks",
            new CreateWebhookSubscriptionRequest("https://order-x.invalid/h", 1L, null));
        await Task.Delay(20);
        await c.PostAsJsonAsync("/api/v1/webhooks",
            new CreateWebhookSubscriptionRequest("https://order-y.invalid/h", 1L, null));

        var list = await (await c.GetAsync("/api/v1/webhooks"))
            .Content.ReadFromJsonAsync<List<WebhookSubscriptionDto>>();
        var idxX = list!.FindIndex(s => s.TargetUrl == "https://order-x.invalid/h");
        var idxY = list.FindIndex(s => s.TargetUrl == "https://order-y.invalid/h");
        idxY.Should().BeLessThan(idxX);
    }

    [Fact]
    public async Task Comment_POST_with_InternalService_principal_leaves_AuthorUserId_null()
    {
        await using var f = new WaypointApiFactory
        {
            PostgresConnectionString = _pg.ConnectionString,
            TestPrincipal = new Principal(
                PrincipalKind.InternalService,
                Guid.NewGuid().ToString(), "svc", []),
        };
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        await c.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("cmsvc", "p", "CMSVC"));
        await c.PostAsJsonAsync("/api/v1/projects/cmsvc/issues", new CreateIssueRequest("t", "b"));

        var resp = await c.PostAsJsonAsync("/api/v1/projects/cmsvc/issues/1/comments",
            new CreateCommentRequest("written-by-service"));
        var dto = await resp.Content.ReadFromJsonAsync<CommentDto>();
        dto!.AuthorUserId.Should().BeNull();
    }
}
