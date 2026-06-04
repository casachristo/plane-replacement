using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

public class AcceptanceCriterionEndpointsTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public AcceptanceCriterionEndpointsTests(PostgresFixture pg) => _pg = pg;

    private static async Task<HttpClient> ProjectWithOneIssue(PostgresFixture pg, string slug, string ident)
    {
        var factory = new WaypointApiFactory { PostgresConnectionString = pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/projects",
            new CreateProjectRequest(slug, $"Test {slug}", ident));
        await client.PostAsJsonAsync($"/api/v1/projects/{slug}/issues",
            new CreateIssueRequest("Issue with AC", "body"));
        return client;
    }

    [Fact]
    public async Task POST_creates_AC_with_auto_position_1()
    {
        using var client = await ProjectWithOneIssue(_pg, "ac1", "AC1");

        var resp = await client.PostAsJsonAsync("/api/v1/projects/ac1/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("Login button is visible"));
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await resp.Content.ReadFromJsonAsync<AcceptanceCriterionDto>();
        dto!.Position.Should().Be(1);
        dto.Text.Should().Be("Login button is visible");
        dto.Checked.Should().BeFalse();
        dto.CheckedAt.Should().BeNull();
    }

    [Fact]
    public async Task POST_appends_at_max_position_plus_one()
    {
        using var client = await ProjectWithOneIssue(_pg, "ac2", "AC2");

        await client.PostAsJsonAsync("/api/v1/projects/ac2/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("first"));
        await client.PostAsJsonAsync("/api/v1/projects/ac2/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("second"));
        var third = await (await client.PostAsJsonAsync("/api/v1/projects/ac2/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("third"))).Content.ReadFromJsonAsync<AcceptanceCriterionDto>();

        third!.Position.Should().Be(3);
    }

    [Fact]
    public async Task POST_returns_422_when_text_empty()
    {
        using var client = await ProjectWithOneIssue(_pg, "ac3", "AC3");

        var resp = await client.PostAsJsonAsync("/api/v1/projects/ac3/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("   "));
        ((int)resp.StatusCode).Should().Be(422);
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Error.Code.Should().Be("text_required");
    }

    [Fact]
    public async Task GET_returns_ACs_ordered_by_position()
    {
        using var client = await ProjectWithOneIssue(_pg, "ac4", "AC4");

        await client.PostAsJsonAsync("/api/v1/projects/ac4/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("third", Position: 3));
        await client.PostAsJsonAsync("/api/v1/projects/ac4/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("first", Position: 1));
        await client.PostAsJsonAsync("/api/v1/projects/ac4/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("second", Position: 2));

        var listResp = await client.GetAsync("/api/v1/projects/ac4/issues/1/acceptance-criteria");
        var list = await listResp.Content.ReadFromJsonAsync<List<AcceptanceCriterionDto>>();
        list!.Select(a => a.Text).Should().Equal("first", "second", "third");
    }

    [Fact]
    public async Task PATCH_updates_text_only()
    {
        using var client = await ProjectWithOneIssue(_pg, "ac5", "AC5");
        var created = await (await client.PostAsJsonAsync("/api/v1/projects/ac5/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("original"))).Content.ReadFromJsonAsync<AcceptanceCriterionDto>();

        var resp = await client.PatchAsJsonAsync(
            $"/api/v1/projects/ac5/issues/1/acceptance-criteria/{created!.Id}",
            new UpdateAcceptanceCriterionRequest(Text: "edited"));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await resp.Content.ReadFromJsonAsync<AcceptanceCriterionDto>();
        updated!.Text.Should().Be("edited");
        updated.Position.Should().Be(created.Position);  // unchanged
    }

    [Fact]
    public async Task POST_check_marks_checked_and_records_actor()
    {
        using var client = await ProjectWithOneIssue(_pg, "ac6", "AC6");
        var created = await (await client.PostAsJsonAsync("/api/v1/projects/ac6/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("login works"))).Content.ReadFromJsonAsync<AcceptanceCriterionDto>();

        var resp = await client.PostAsync(
            $"/api/v1/projects/ac6/issues/1/acceptance-criteria/{created!.Id}/check", content: null);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var checkedDto = await resp.Content.ReadFromJsonAsync<AcceptanceCriterionDto>();
        checkedDto!.Checked.Should().BeTrue();
        checkedDto.CheckedAt.Should().NotBeNull();
        checkedDto.CheckedByActorType.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task POST_uncheck_clears_actor_fields()
    {
        using var client = await ProjectWithOneIssue(_pg, "ac7", "AC7");
        var created = await (await client.PostAsJsonAsync("/api/v1/projects/ac7/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("toggle me"))).Content.ReadFromJsonAsync<AcceptanceCriterionDto>();

        await client.PostAsync($"/api/v1/projects/ac7/issues/1/acceptance-criteria/{created!.Id}/check", content: null);
        var resp = await client.PostAsync($"/api/v1/projects/ac7/issues/1/acceptance-criteria/{created.Id}/uncheck", content: null);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await resp.Content.ReadFromJsonAsync<AcceptanceCriterionDto>();
        dto!.Checked.Should().BeFalse();
        dto.CheckedAt.Should().BeNull();
        dto.CheckedByActorType.Should().BeNull();
        dto.CheckedByActorId.Should().BeNull();
        dto.CheckedByActorLabel.Should().BeNull();
    }

    [Fact]
    public async Task DELETE_removes_AC_from_subsequent_GET()
    {
        using var client = await ProjectWithOneIssue(_pg, "ac8", "AC8");
        var created = await (await client.PostAsJsonAsync("/api/v1/projects/ac8/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("temp"))).Content.ReadFromJsonAsync<AcceptanceCriterionDto>();

        var del = await client.DeleteAsync($"/api/v1/projects/ac8/issues/1/acceptance-criteria/{created!.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var list = await (await client.GetAsync("/api/v1/projects/ac8/issues/1/acceptance-criteria"))
            .Content.ReadFromJsonAsync<List<AcceptanceCriterionDto>>();
        list!.Should().BeEmpty();
    }

    [Fact]
    public async Task GET_returns_404_when_issue_missing()
    {
        using var client = await ProjectWithOneIssue(_pg, "ac9", "AC9");
        var resp = await client.GetAsync("/api/v1/projects/ac9/issues/9999/acceptance-criteria");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Issue_GET_includes_AC_inline_in_position_order()
    {
        using var client = await ProjectWithOneIssue(_pg, "acin", "ACIN");
        await client.PostAsJsonAsync("/api/v1/projects/acin/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("alpha"));
        await client.PostAsJsonAsync("/api/v1/projects/acin/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("beta"));

        var issue = await (await client.GetAsync("/api/v1/projects/acin/issues/1"))
            .Content.ReadFromJsonAsync<IssueDto>();
        issue!.AcceptanceCriteria.Should().HaveCount(2);
        issue.AcceptanceCriteria.Select(a => a.Text).Should().Equal("alpha", "beta");
        issue.AcceptanceCriteria.All(a => !a.Checked).Should().BeTrue();
    }
}
