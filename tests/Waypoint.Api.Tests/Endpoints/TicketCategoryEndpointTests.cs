using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

// WAY-24: first-class ticket categories on the issue create/list API.
public class TicketCategoryEndpointTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public TicketCategoryEndpointTests(PostgresFixture pg) => _pg = pg;

    private async Task<WaypointApiFactory> Project(string slug, string ident)
    {
        var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        await c.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest(slug, slug, ident));
        return f;
    }

    [Fact]
    public async Task Create_with_category_round_trips_on_the_dto()
    {
        await using var f = await Project("cat-a", "CTA");
        using var c = f.CreateClient();
        var dto = await (await c.PostAsJsonAsync("/api/v1/projects/cat-a/issues",
            new CreateIssueRequest("a bug", "body", Category: "bug"))).Content.ReadFromJsonAsync<IssueDto>();
        dto!.Category.Should().Be("bug");
    }

    [Fact]
    public async Task Create_without_category_defaults_to_feature()
    {
        await using var f = await Project("cat-b", "CTB");
        using var c = f.CreateClient();
        var dto = await (await c.PostAsJsonAsync("/api/v1/projects/cat-b/issues",
            new CreateIssueRequest("plain", "body"))).Content.ReadFromJsonAsync<IssueDto>();
        dto!.Category.Should().Be("feature");
    }

    [Fact]
    public async Task Create_with_unknown_category_is_rejected_422()
    {
        await using var f = await Project("cat-c", "CTC");
        using var c = f.CreateClient();
        var resp = await c.PostAsJsonAsync("/api/v1/projects/cat-c/issues",
            new CreateIssueRequest("bad", "body", Category: "nonsense"));
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task List_can_filter_by_category()
    {
        await using var f = await Project("cat-d", "CTD");
        using var c = f.CreateClient();
        await c.PostAsJsonAsync("/api/v1/projects/cat-d/issues", new CreateIssueRequest("b1", "x", Category: "bug"));
        await c.PostAsJsonAsync("/api/v1/projects/cat-d/issues", new CreateIssueRequest("f1", "x", Category: "feature"));
        await c.PostAsJsonAsync("/api/v1/projects/cat-d/issues", new CreateIssueRequest("b2", "x", Category: "bug"));

        var page = await (await c.GetAsync("/api/v1/projects/cat-d/issues?category=bug"))
            .Content.ReadFromJsonAsync<PagedResponse<IssueDto>>();
        page!.Data.Should().HaveCount(2);
        page.Data.Should().OnlyContain(i => i.Category == "bug");

        var bad = await c.GetAsync("/api/v1/projects/cat-d/issues?category=nope");
        bad.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}
