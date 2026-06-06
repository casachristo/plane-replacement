using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

/// <summary>
/// Mutation-coverage for SearchEndpoints (/api/v1/search). Exercises the real Postgres
/// full-text path (search_vector @@ plainto_tsquery), the empty-query validation guard,
/// the project filter branch, the limit clamp, and the auth guard.
/// </summary>
public class SearchEndpointsMutationCoverage : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public SearchEndpointsMutationCoverage(PostgresFixture pg) => _pg = pg;

    private sealed record SearchHitDto(Guid Id, string ProjectSlug, string ProjectIdentifier,
        int Sequence, string Title, string Snippet, float Rank);

    private static async Task CreateProject(HttpClient c, string slug, string identifier) =>
        (await c.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest(slug, slug + " project", identifier)))
            .EnsureSuccessStatusCode();

    private static async Task CreateIssue(HttpClient c, string slug, string title, string body) =>
        (await c.PostAsJsonAsync($"/api/v1/projects/{slug}/issues", new CreateIssueRequest(title, body)))
            .EnsureSuccessStatusCode();

    private static async Task<List<SearchHitDto>> Search(HttpClient c, string query)
    {
        var resp = await c.GetAsync(query);
        var body = await resp.Content.ReadAsStringAsync();
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "search should succeed but body was: {0}", body);
        return System.Text.Json.JsonSerializer.Deserialize<List<SearchHitDto>>(body,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
    }

    [Fact]
    public async Task Search_returns_matching_issue_with_project_and_sequence()
    {
        await using var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        await CreateProject(c, "srcha", "SRA");
        await CreateIssue(c, "srcha", "Payment gateway timeout", "the gateway keeps timing out on checkout");

        var hits = await Search(c, "/api/v1/search?q=gateway");

        var hit = hits.Should().ContainSingle(h => h.ProjectSlug == "srcha").Subject;
        hit.ProjectIdentifier.Should().Be("SRA");
        hit.Sequence.Should().Be(1);
        hit.Title.Should().Contain("gateway");
        hit.Rank.Should().BeGreaterThan(0f);
    }

    [Fact]
    public async Task Search_with_blank_query_returns_422_query_required()
    {
        await using var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();

        var resp = await c.GetAsync("/api/v1/search?q=%20");

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("query_required");
        body.Should().Contain("Provide a search query");
    }

    [Fact]
    public async Task Search_project_filter_excludes_other_projects()
    {
        await using var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        await CreateProject(c, "filtera", "FLA");
        await CreateProject(c, "filterb", "FLB");
        await CreateIssue(c, "filtera", "Database deadlock alpha", "deadlock in the database under load");
        await CreateIssue(c, "filterb", "Database deadlock beta", "deadlock in the database under load");

        var hits = await Search(c, "/api/v1/search?q=deadlock&project=filtera");

        hits.Should().NotBeNullOrEmpty();
        hits.Should().OnlyContain(h => h.ProjectSlug == "filtera");
    }

    [Fact]
    public async Task Search_respects_limit_clamp()
    {
        await using var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        await CreateProject(c, "limita", "LMA");
        await CreateIssue(c, "limita", "Cache eviction one", "cache eviction storm observed");
        await CreateIssue(c, "limita", "Cache eviction two", "cache eviction storm observed");
        await CreateIssue(c, "limita", "Cache eviction three", "cache eviction storm observed");

        var hits = await Search(c, "/api/v1/search?q=eviction&limit=2");

        hits.Should().HaveCount(2);
    }

    [Fact]
    public async Task Search_without_principal_returns_401()
    {
        await using var f = new WaypointApiFactory
        {
            PostgresConnectionString = _pg.ConnectionString,
            TestPrincipal = null!,
        };
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();

        var resp = await c.GetAsync("/api/v1/search?q=anything");

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
