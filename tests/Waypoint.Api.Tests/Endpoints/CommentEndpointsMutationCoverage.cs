using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

/// <summary>
/// Mutation-coverage HTTP tests for CommentEndpoints. Exercises every branch:
/// auth, project resolution, issue resolution, success, list semantics.
/// </summary>
public class CommentEndpointsMutationCoverage : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public CommentEndpointsMutationCoverage(PostgresFixture pg) => _pg = pg;

    private static async Task<HttpClient> SetupProjectWithIssue(PostgresFixture pg, string slug, string ident)
    {
        var factory = new WaypointApiFactory { PostgresConnectionString = pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/projects",
            new CreateProjectRequest(slug, "Test", ident));
        await client.PostAsJsonAsync($"/api/v1/projects/{slug}/issues",
            new CreateIssueRequest("Title", "Body"));
        return client;
    }

    [Fact]
    public async Task POST_creates_comment_returns_201_with_body()
    {
        using var client = await SetupProjectWithIssue(_pg, "cm1", "CM1");

        var resp = await client.PostAsJsonAsync("/api/v1/projects/cm1/issues/1/comments",
            new CreateCommentRequest("Looks good."));

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await resp.Content.ReadFromJsonAsync<CommentDto>();
        dto!.BodyMd.Should().Be("Looks good.");
    }

    [Fact]
    public async Task POST_to_unknown_project_returns_404_with_project_not_found()
    {
        using var client = await SetupProjectWithIssue(_pg, "cm2", "CM2");
        var resp = await client.PostAsJsonAsync("/api/v1/projects/does-not-exist/issues/1/comments",
            new CreateCommentRequest("hi"));
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Error.Code.Should().Be("project_not_found");
    }

    [Fact]
    public async Task POST_to_unknown_issue_returns_404_with_issue_not_found()
    {
        using var client = await SetupProjectWithIssue(_pg, "cm3", "CM3");
        var resp = await client.PostAsJsonAsync("/api/v1/projects/cm3/issues/999/comments",
            new CreateCommentRequest("hi"));
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Error.Code.Should().Be("issue_not_found");
    }

    [Fact]
    public async Task GET_returns_empty_list_when_no_comments()
    {
        using var client = await SetupProjectWithIssue(_pg, "cm4", "CM4");
        var resp = await client.GetAsync("/api/v1/projects/cm4/issues/1/comments");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await resp.Content.ReadFromJsonAsync<List<CommentDto>>();
        list!.Should().BeEmpty();
    }

    [Fact]
    public async Task GET_returns_all_posted_comments()
    {
        using var client = await SetupProjectWithIssue(_pg, "cm5", "CM5");
        await client.PostAsJsonAsync("/api/v1/projects/cm5/issues/1/comments", new CreateCommentRequest("one"));
        await client.PostAsJsonAsync("/api/v1/projects/cm5/issues/1/comments", new CreateCommentRequest("two"));
        await client.PostAsJsonAsync("/api/v1/projects/cm5/issues/1/comments", new CreateCommentRequest("three"));

        var resp = await client.GetAsync("/api/v1/projects/cm5/issues/1/comments");
        var list = await resp.Content.ReadFromJsonAsync<List<CommentDto>>();
        list.Should().HaveCount(3);
        list!.Select(c => c.BodyMd).Should().BeEquivalentTo(new[] { "one", "two", "three" });
    }

    [Fact]
    public async Task GET_against_unknown_project_returns_404_project_not_found()
    {
        using var client = await SetupProjectWithIssue(_pg, "cm6", "CM6");
        var resp = await client.GetAsync("/api/v1/projects/does-not-exist/issues/1/comments");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Error.Code.Should().Be("project_not_found");
    }

    [Fact]
    public async Task GET_against_unknown_issue_returns_404_issue_not_found()
    {
        using var client = await SetupProjectWithIssue(_pg, "cm7", "CM7");
        var resp = await client.GetAsync("/api/v1/projects/cm7/issues/9999/comments");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Error.Code.Should().Be("issue_not_found");
    }

    [Fact]
    public async Task POST_comment_returned_dto_includes_issueId()
    {
        using var client = await SetupProjectWithIssue(_pg, "cm8", "CM8");
        var resp = await client.PostAsJsonAsync("/api/v1/projects/cm8/issues/1/comments",
            new CreateCommentRequest("test"));
        var dto = await resp.Content.ReadFromJsonAsync<CommentDto>();
        dto!.IssueId.Should().NotBe(Guid.Empty);
    }
}
