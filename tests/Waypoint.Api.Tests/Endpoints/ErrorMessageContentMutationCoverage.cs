using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using FluentAssertions;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

/// <summary>
/// Error-message-content tests across all endpoints. Pin error-message string
/// content to kill the "$\"\"" interpolated-string mutations Stryker generates.
/// </summary>
public class ErrorMessageContentMutationCoverage : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public ErrorMessageContentMutationCoverage(PostgresFixture pg) => _pg = pg;

    private async Task<HttpClient> NewClient(string slug, string ident)
    {
        var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        var c = f.CreateClient();
        await c.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest(slug, "p", ident));
        return c;
    }

    [Fact]
    public async Task Comment_unknown_project_message_includes_slug()
    {
        using var c = await NewClient("emc1", "EMC1");
        var resp = await c.PostAsJsonAsync(
            "/api/v1/projects/zzz-no-project-emc1/issues/1/comments",
            new CreateCommentRequest("hi"));
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Error.Message.Should().Contain("zzz-no-project-emc1");
    }

    [Fact]
    public async Task Comment_unknown_issue_message_includes_identifier_and_seq()
    {
        using var c = await NewClient("emc2", "EMC2");
        var resp = await c.PostAsJsonAsync(
            "/api/v1/projects/emc2/issues/9999/comments",
            new CreateCommentRequest("hi"));
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Error.Message.Should().Contain("EMC2").And.Contain("9999");
    }

    [Fact]
    public async Task AC_unknown_project_message_includes_slug()
    {
        using var c = await NewClient("emc3", "EMC3");
        var resp = await c.PostAsJsonAsync(
            "/api/v1/projects/zzz-no-project-emc3/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("x"));
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Error.Message.Should().Contain("zzz-no-project-emc3");
    }

    [Fact]
    public async Task AC_unknown_issue_message_includes_identifier_and_seq()
    {
        using var c = await NewClient("emc4", "EMC4");
        var resp = await c.PostAsJsonAsync(
            "/api/v1/projects/emc4/issues/9876/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("x"));
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Error.Message.Should().Contain("EMC4").And.Contain("9876");
    }

    [Fact]
    public async Task AC_unknown_id_message_includes_the_guid()
    {
        using var c = await NewClient("emc5", "EMC5");
        await c.PostAsJsonAsync("/api/v1/projects/emc5/issues",
            new CreateIssueRequest("t", "b"));
        var missingId = Guid.NewGuid();
        var resp = await c.PatchAsJsonAsync(
            $"/api/v1/projects/emc5/issues/1/acceptance-criteria/{missingId}",
            new UpdateAcceptanceCriterionRequest(Text: "x"));
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Error.Message.Should().Contain(missingId.ToString());
    }

    [Fact]
    public async Task State_wrong_project_error_message_says_belong_or_project()
    {
        await using var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        await c.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("emc6a", "P", "EMC6A"));
        await c.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("emc6b", "P", "EMC6B"));
        await c.PostAsJsonAsync("/api/v1/projects/emc6a/issues", new CreateIssueRequest("t", "b"));

        Guid otherState;
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Waypoint.Domain.WaypointDbContext>();
            var b = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .FirstAsync(db.Projects, p => p.Slug == "emc6b");
            otherState = b.DefaultStateId!.Value;
        }

        var resp = await c.PostAsJsonAsync("/api/v1/projects/emc6a/issues/1/transitions",
            new TransitionIssueRequest(otherState));
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Error.Message.ToLowerInvariant().Should().ContainAny("project", "belong");
    }
}
