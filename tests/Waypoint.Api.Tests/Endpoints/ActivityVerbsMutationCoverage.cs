using System.Net.Http.Json;
using FluentAssertions;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

public class ActivityVerbsMutationCoverage : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public ActivityVerbsMutationCoverage(PostgresFixture pg) => _pg = pg;

    private async Task<HttpClient> Setup(string slug, string ident)
    {
        var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        var c = f.CreateClient();
        await c.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest(slug, "p", ident));
        await c.PostAsJsonAsync($"/api/v1/projects/{slug}/issues",
            new CreateIssueRequest("t", "b"));
        return c;
    }

    [Fact]
    public async Task POST_issue_creates_an_activity_with_Verb_created()
    {
        using var c = await Setup("av1", "AV1");
        var events = await (await c.GetAsync("/api/v1/projects/av1/issues/1/activity"))
            .Content.ReadFromJsonAsync<List<ActivityDto>>();
        events!.Any(e => e.Verb == "created").Should().BeTrue();
    }

    [Fact]
    public async Task PATCH_issue_writes_an_activity_with_Verb_updated()
    {
        using var c = await Setup("av2", "AV2");
        await c.PatchAsJsonAsync("/api/v1/projects/av2/issues/1",
            new UpdateIssueRequest(Title: "new"));
        var events = await (await c.GetAsync("/api/v1/projects/av2/issues/1/activity"))
            .Content.ReadFromJsonAsync<List<ActivityDto>>();
        events!.Any(e => e.Verb == "updated").Should().BeTrue();
    }

    [Fact]
    public async Task GET_activity_DTOs_have_non_null_At_timestamp()
    {
        using var c = await Setup("av3", "AV3");
        var events = await (await c.GetAsync("/api/v1/projects/av3/issues/1/activity"))
            .Content.ReadFromJsonAsync<List<ActivityDto>>();
        events!.Should().NotBeEmpty();
        events!.All(e => e.At > DateTimeOffset.MinValue).Should().BeTrue();
    }

    [Fact]
    public async Task POST_issue_creation_activity_ActorType_is_System()
    {
        using var c = await Setup("av4", "AV4");
        var events = await (await c.GetAsync("/api/v1/projects/av4/issues/1/activity"))
            .Content.ReadFromJsonAsync<List<ActivityDto>>();
        var created = events!.FirstOrDefault(e => e.Verb == "created");
        created.Should().NotBeNull();
        created!.ActorType.Should().Be("System");
    }
}
