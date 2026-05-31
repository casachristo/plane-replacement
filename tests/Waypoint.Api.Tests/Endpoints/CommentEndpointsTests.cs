using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

public class CommentEndpointsTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public CommentEndpointsTests(PostgresFixture pg) => _pg = pg;

    [Fact]
    public async Task POST_then_GET_round_trips_a_comment()
    {
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("cmt-proj", "C", "CP1"));
        var issue = await (await client.PostAsJsonAsync("/api/v1/projects/cmt-proj/issues",
            new CreateIssueRequest("I", ""))).Content.ReadFromJsonAsync<IssueDto>();

        var resp = await client.PostAsJsonAsync($"/api/v1/projects/cmt-proj/issues/{issue!.Sequence}/comments",
            new CreateCommentRequest("Hello **comment**"));
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await resp.Content.ReadFromJsonAsync<CommentDto>();
        dto!.BodyMd.Should().Be("Hello **comment**");

        var listResp = await client.GetAsync($"/api/v1/projects/cmt-proj/issues/{issue.Sequence}/comments");
        listResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await listResp.Content.ReadFromJsonAsync<List<CommentDto>>();
        list.Should().NotBeNull().And.HaveCount(1);
        list![0].BodyMd.Should().Be("Hello **comment**");
    }
}
