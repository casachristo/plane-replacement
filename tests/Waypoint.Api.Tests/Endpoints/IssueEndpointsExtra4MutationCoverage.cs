using System.Net.Http.Json;
using FluentAssertions;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

public class IssueEndpointsExtra4MutationCoverage : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public IssueEndpointsExtra4MutationCoverage(PostgresFixture pg) => _pg = pg;

    private async Task<HttpClient> Setup(string slug, string ident)
    {
        var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        var c = f.CreateClient();
        await c.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest(slug, "p", ident));
        return c;
    }

    [Fact]
    public async Task POST_then_GET_returns_DTO_with_matching_Title()
    {
        using var c = await Setup("ix4a", "IX4A");
        await c.PostAsJsonAsync("/api/v1/projects/ix4a/issues", new CreateIssueRequest("X-Title", "body"));
        var dto = await (await c.GetAsync("/api/v1/projects/ix4a/issues/1")).Content.ReadFromJsonAsync<IssueDto>();
        dto!.Title.Should().Be("X-Title");
    }

    [Fact]
    public async Task POST_returned_DTO_StateName_is_ToDo_for_fresh_project()
    {
        using var c = await Setup("ix4b", "IX4B");
        var dto = await (await c.PostAsJsonAsync("/api/v1/projects/ix4b/issues",
            new CreateIssueRequest("t", "b"))).Content.ReadFromJsonAsync<IssueDto>();
        dto!.StateName.Should().Be("To Do");
    }

    [Fact]
    public async Task PATCH_with_null_fields_keeps_existing_values_unchanged()
    {
        using var c = await Setup("ix4c", "IX4C");
        await c.PostAsJsonAsync("/api/v1/projects/ix4c/issues", new CreateIssueRequest("Keep", "KeepBody"));
        var resp = await c.PatchAsJsonAsync("/api/v1/projects/ix4c/issues/1",
            new UpdateIssueRequest());   // all nulls
        var dto = await resp.Content.ReadFromJsonAsync<IssueDto>();
        dto!.Title.Should().Be("Keep");
        dto.DescriptionMd.Should().Be("KeepBody");
    }

    [Fact]
    public async Task POST_issue_returned_DTO_IssueTypeName_is_default_type_name()
    {
        using var c = await Setup("ix4d", "IX4D");
        var dto = await (await c.PostAsJsonAsync("/api/v1/projects/ix4d/issues",
            new CreateIssueRequest("t", "b"))).Content.ReadFromJsonAsync<IssueDto>();
        dto!.IssueTypeName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GET_list_data_DTOs_carry_their_StateName()
    {
        using var c = await Setup("ix4e", "IX4E");
        await c.PostAsJsonAsync("/api/v1/projects/ix4e/issues", new CreateIssueRequest("Alpha", "a"));
        await c.PostAsJsonAsync("/api/v1/projects/ix4e/issues", new CreateIssueRequest("Beta", "b"));
        var page = await (await c.GetAsync("/api/v1/projects/ix4e/issues"))
            .Content.ReadFromJsonAsync<PagedResponse<IssueDto>>();
        page!.Data.All(i => i.StateName == "To Do").Should().BeTrue();
    }

    [Fact]
    public async Task POST_issue_with_explicit_IssueTypeId_uses_that_type()
    {
        using var c = await Setup("ix4f", "IX4F");
        // Get the default IssueType id by fetching an issue first.
        var seed = await (await c.PostAsJsonAsync("/api/v1/projects/ix4f/issues",
            new CreateIssueRequest("seed", "s"))).Content.ReadFromJsonAsync<IssueDto>();
        var resp = await c.PostAsJsonAsync("/api/v1/projects/ix4f/issues",
            new CreateIssueRequest("with-type", "body", IssueTypeId: seed!.IssueTypeId));
        var dto = await resp.Content.ReadFromJsonAsync<IssueDto>();
        dto!.IssueTypeId.Should().Be(seed.IssueTypeId);
    }
}
