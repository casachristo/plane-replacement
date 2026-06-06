using System.Net.Http.Json;
using FluentAssertions;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

public class AcceptanceCriterionEndpointsExtra3MutationCoverage : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public AcceptanceCriterionEndpointsExtra3MutationCoverage(PostgresFixture pg) => _pg = pg;

    private async Task<HttpClient> Setup(string slug, string ident)
    {
        var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        var c = f.CreateClient();
        await c.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest(slug, "p", ident));
        await c.PostAsJsonAsync($"/api/v1/projects/{slug}/issues", new CreateIssueRequest("t", "b"));
        return c;
    }

    [Fact]
    public async Task POST_AC_returned_DTO_has_Id_assigned()
    {
        using var c = await Setup("ax3a", "AX3A");
        var dto = await (await c.PostAsJsonAsync(
            "/api/v1/projects/ax3a/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("hello"))).Content.ReadFromJsonAsync<AcceptanceCriterionDto>();
        dto!.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task POST_AC_returned_DTO_has_recent_CreatedAt()
    {
        using var c = await Setup("ax3b", "AX3B");
        var dto = await (await c.PostAsJsonAsync(
            "/api/v1/projects/ax3b/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("hello"))).Content.ReadFromJsonAsync<AcceptanceCriterionDto>();
        dto!.CreatedAt.Should().BeAfter(DateTimeOffset.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task POST_AC_returned_DTO_Text_matches_request()
    {
        using var c = await Setup("ax3c", "AX3C");
        var dto = await (await c.PostAsJsonAsync(
            "/api/v1/projects/ax3c/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("specific-text-123"))).Content.ReadFromJsonAsync<AcceptanceCriterionDto>();
        dto!.Text.Should().Be("specific-text-123");
    }

    [Fact]
    public async Task POST_AC_Location_header_includes_acceptance_criteria_path()
    {
        using var c = await Setup("ax3d", "AX3D");
        var resp = await c.PostAsJsonAsync(
            "/api/v1/projects/ax3d/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("loc"));
        resp.Headers.Location?.OriginalString.Should().Contain("acceptance-criteria");
    }

    [Fact]
    public async Task POST_AC_position_defaults_increment_consecutively()
    {
        using var c = await Setup("ax3e", "AX3E");
        var a = await (await c.PostAsJsonAsync(
            "/api/v1/projects/ax3e/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("a"))).Content.ReadFromJsonAsync<AcceptanceCriterionDto>();
        var b = await (await c.PostAsJsonAsync(
            "/api/v1/projects/ax3e/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("b"))).Content.ReadFromJsonAsync<AcceptanceCriterionDto>();
        b!.Position.Should().Be(a!.Position + 1);
    }
}
