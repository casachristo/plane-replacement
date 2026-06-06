using Waypoint.Api.Endpoints.PublicApi;
using System.Net.Http.Json;
using FluentAssertions;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

public class MoreMutationCoverage1 : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public MoreMutationCoverage1(PostgresFixture pg) => _pg = pg;

    [Fact]
    public async Task Project_POST_returned_DTO_Slug_matches_request_exactly()
    {
        await using var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var dto = await (await c.PostAsJsonAsync("/api/v1/projects",
            new CreateProjectRequest("specific-slug-text", "N", "SST"))).Content.ReadFromJsonAsync<ProjectDto>();
        dto!.Slug.Should().Be("specific-slug-text");
    }

    [Fact]
    public async Task Comment_POST_returned_DTO_has_recent_CreatedAt()
    {
        await using var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        await c.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("mmcv1", "p", "MMCV1"));
        await c.PostAsJsonAsync("/api/v1/projects/mmcv1/issues", new CreateIssueRequest("t", "b"));
        var dto = await (await c.PostAsJsonAsync("/api/v1/projects/mmcv1/issues/1/comments",
            new CreateCommentRequest("hi"))).Content.ReadFromJsonAsync<CommentDto>();
        dto!.CreatedAt.Should().BeAfter(DateTimeOffset.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task Issue_GET_DTO_Sequence_field_matches_path()
    {
        await using var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        await c.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("mmcv2", "p", "MMCV2"));
        await c.PostAsJsonAsync("/api/v1/projects/mmcv2/issues", new CreateIssueRequest("a", "x"));
        await c.PostAsJsonAsync("/api/v1/projects/mmcv2/issues", new CreateIssueRequest("b", "y"));
        var dto = await (await c.GetAsync("/api/v1/projects/mmcv2/issues/2"))
            .Content.ReadFromJsonAsync<IssueDto>();
        dto!.Sequence.Should().Be(2);
    }

    [Fact]
    public async Task Webhook_DELETE_then_GET_excludes_deleted()
    {
        await using var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var created = await (await c.PostAsJsonAsync("/api/v1/webhooks",
            new CreateWebhookSubscriptionRequest("https://gone.invalid/h", 1L, null)))
            .Content.ReadFromJsonAsync<Waypoint.Api.Endpoints.PublicApi.WebhookSubscriptionCreatedDto>();
        await c.DeleteAsync($"/api/v1/webhooks/{created!.Subscription.Id}");
        var list = await (await c.GetAsync("/api/v1/webhooks"))
            .Content.ReadFromJsonAsync<List<Waypoint.Api.Endpoints.PublicApi.WebhookSubscriptionDto>>();
        list!.Any(s => s.Id == created.Subscription.Id).Should().BeFalse();
    }
}
