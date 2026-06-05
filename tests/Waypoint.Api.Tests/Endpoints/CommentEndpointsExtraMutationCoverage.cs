using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

public class CommentEndpointsExtraMutationCoverage : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public CommentEndpointsExtraMutationCoverage(PostgresFixture pg) => _pg = pg;

    private async Task<HttpClient> NewClient(string slug, string ident)
    {
        var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        var c = f.CreateClient();
        await c.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest(slug, "p", ident));
        await c.PostAsJsonAsync($"/api/v1/projects/{slug}/issues",
            new CreateIssueRequest("title", "body"));
        return c;
    }

    [Fact]
    public async Task POST_comment_returned_DTO_has_BodyMd_set_to_request_value()
    {
        using var client = await NewClient("cmx1", "CMX1");
        var resp = await client.PostAsJsonAsync("/api/v1/projects/cmx1/issues/1/comments",
            new CreateCommentRequest("exact body text 12345"));
        var dto = await resp.Content.ReadFromJsonAsync<CommentDto>();
        dto!.BodyMd.Should().Be("exact body text 12345");
    }

    [Fact]
    public async Task POST_comment_returned_DTO_has_AuthorUserId_set_for_Human_principal()
    {
        // Default TestPrincipal is Human with a known Id; AuthorUserId must be set.
        using var client = await NewClient("cmx2", "CMX2");
        var resp = await client.PostAsJsonAsync("/api/v1/projects/cmx2/issues/1/comments",
            new CreateCommentRequest("hi"));
        var dto = await resp.Content.ReadFromJsonAsync<CommentDto>();
        dto!.AuthorUserId.Should().NotBeNull();
    }

    [Fact]
    public async Task POST_comment_returned_DTO_has_non_empty_Id()
    {
        using var client = await NewClient("cmx3", "CMX3");
        var resp = await client.PostAsJsonAsync("/api/v1/projects/cmx3/issues/1/comments",
            new CreateCommentRequest("hi"));
        var dto = await resp.Content.ReadFromJsonAsync<CommentDto>();
        dto!.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task POST_comment_returned_DTO_CreatedAt_is_recent()
    {
        using var client = await NewClient("cmx4", "CMX4");
        var resp = await client.PostAsJsonAsync("/api/v1/projects/cmx4/issues/1/comments",
            new CreateCommentRequest("hi"));
        var dto = await resp.Content.ReadFromJsonAsync<CommentDto>();
        dto!.CreatedAt.Should().BeAfter(DateTimeOffset.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task GET_list_DTOs_carry_the_body_text_set_at_create()
    {
        using var client = await NewClient("cmx5", "CMX5");
        await client.PostAsJsonAsync("/api/v1/projects/cmx5/issues/1/comments",
            new CreateCommentRequest("alpha-body"));
        await client.PostAsJsonAsync("/api/v1/projects/cmx5/issues/1/comments",
            new CreateCommentRequest("beta-body"));

        var resp = await client.GetAsync("/api/v1/projects/cmx5/issues/1/comments");
        var list = await resp.Content.ReadFromJsonAsync<List<CommentDto>>();
        list!.Select(c => c.BodyMd).Should().Contain("alpha-body").And.Contain("beta-body");
    }
}
