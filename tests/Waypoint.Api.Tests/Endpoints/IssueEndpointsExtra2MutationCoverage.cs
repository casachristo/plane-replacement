using System.Net.Http.Json;
using FluentAssertions;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

public class IssueEndpointsExtra2MutationCoverage : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public IssueEndpointsExtra2MutationCoverage(PostgresFixture pg) => _pg = pg;

    private async Task<HttpClient> NewClient(string slug, string ident)
    {
        var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        var c = f.CreateClient();
        await c.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest(slug, "p", ident));
        return c;
    }

    [Fact]
    public async Task POST_issue_returned_DTO_has_Title_from_request()
    {
        using var client = await NewClient("ix2a", "IX2A");
        var dto = await (await client.PostAsJsonAsync("/api/v1/projects/ix2a/issues",
            new CreateIssueRequest("This is the title", "body"))).Content.ReadFromJsonAsync<IssueDto>();
        dto!.Title.Should().Be("This is the title");
    }

    [Fact]
    public async Task POST_issue_returned_DTO_has_DescriptionMd_from_request()
    {
        using var client = await NewClient("ix2b", "IX2B");
        var dto = await (await client.PostAsJsonAsync("/api/v1/projects/ix2b/issues",
            new CreateIssueRequest("title", "this is the body **markdown**"))).Content.ReadFromJsonAsync<IssueDto>();
        dto!.DescriptionMd.Should().Be("this is the body **markdown**");
    }

    [Fact]
    public async Task POST_issue_returned_DTO_has_default_Priority_None_zero()
    {
        using var client = await NewClient("ix2c", "IX2C");
        var dto = await (await client.PostAsJsonAsync("/api/v1/projects/ix2c/issues",
            new CreateIssueRequest("t", "b"))).Content.ReadFromJsonAsync<IssueDto>();
        dto!.Priority.Should().Be(0);
    }

    [Fact]
    public async Task POST_issue_returned_DTO_has_Backlog_StateName_by_default()
    {
        using var client = await NewClient("ix2d", "IX2D");
        var dto = await (await client.PostAsJsonAsync("/api/v1/projects/ix2d/issues",
            new CreateIssueRequest("t", "b"))).Content.ReadFromJsonAsync<IssueDto>();
        dto!.StateName.Should().Be("Backlog");
    }

    [Fact]
    public async Task POST_two_issues_get_consecutive_Sequence_1_and_2()
    {
        using var client = await NewClient("ix2e", "IX2E");
        var a = await (await client.PostAsJsonAsync("/api/v1/projects/ix2e/issues",
            new CreateIssueRequest("a", "x"))).Content.ReadFromJsonAsync<IssueDto>();
        var b = await (await client.PostAsJsonAsync("/api/v1/projects/ix2e/issues",
            new CreateIssueRequest("b", "y"))).Content.ReadFromJsonAsync<IssueDto>();
        a!.Sequence.Should().Be(1);
        b!.Sequence.Should().Be(2);
    }

    [Fact]
    public async Task PATCH_title_only_does_not_modify_DescriptionMd()
    {
        using var client = await NewClient("ix2f", "IX2F");
        await client.PostAsJsonAsync("/api/v1/projects/ix2f/issues",
            new CreateIssueRequest("original", "untouched body"));
        var resp = await client.PatchAsJsonAsync("/api/v1/projects/ix2f/issues/1",
            new UpdateIssueRequest(Title: "renamed"));
        var dto = await resp.Content.ReadFromJsonAsync<IssueDto>();
        dto!.Title.Should().Be("renamed");
        dto.DescriptionMd.Should().Be("untouched body");
    }

    [Fact]
    public async Task PATCH_priority_only_does_not_modify_Title()
    {
        using var client = await NewClient("ix2g", "IX2G");
        await client.PostAsJsonAsync("/api/v1/projects/ix2g/issues",
            new CreateIssueRequest("keep-this-title", "body"));
        var resp = await client.PatchAsJsonAsync("/api/v1/projects/ix2g/issues/1",
            new UpdateIssueRequest(Priority: 3));
        var dto = await resp.Content.ReadFromJsonAsync<IssueDto>();
        dto!.Title.Should().Be("keep-this-title");
        dto.Priority.Should().Be(3);
    }
}
