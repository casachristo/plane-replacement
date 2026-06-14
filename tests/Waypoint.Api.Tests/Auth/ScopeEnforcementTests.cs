using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Waypoint.Api.Auth;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Xunit;

namespace Waypoint.Api.Tests.Auth;

// WAY-27: token scopes are enforced on write endpoints. A read-only credential
// (["issue:read"]) is rejected with 403 on any write; a credential holding the
// matching scope succeeds. Previously scopes were decorative outside admin/transition.
public class ScopeEnforcementTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public ScopeEnforcementTests(PostgresFixture pg) => _pg = pg;

    private WaypointApiFactory Factory(params string[] scopes) => new()
    {
        PostgresConnectionString = _pg.ConnectionString,
        TestPrincipal = new Principal(PrincipalKind.InternalService, Guid.NewGuid().ToString(), "svc", scopes),
    };

    [Fact]
    public async Task Read_only_token_cannot_create_an_issue()
    {
        await using var admin = Factory("admin");
        await admin.EnsureMigratedAsync();
        using var ac = admin.CreateClient();
        await ac.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("scope-iss", "S", "SCA"));

        await using var ro = Factory("issue:read");
        using var roc = ro.CreateClient();
        var resp = await roc.PostAsJsonAsync("/api/v1/projects/scope-iss/issues", new CreateIssueRequest("nope", "x"));
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Token_with_issue_create_scope_can_create_an_issue()
    {
        await using var admin = Factory("admin");
        await admin.EnsureMigratedAsync();
        using var ac = admin.CreateClient();
        await ac.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("scope-ok", "S", "SCB"));

        await using var writer = Factory("issue:create");
        using var wc = writer.CreateClient();
        var resp = await wc.PostAsJsonAsync("/api/v1/projects/scope-ok/issues", new CreateIssueRequest("yes", "x"));
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Read_only_token_cannot_comment_or_add_acceptance_criteria()
    {
        await using var admin = Factory("admin");
        await admin.EnsureMigratedAsync();
        using var ac = admin.CreateClient();
        await ac.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("scope-w", "S", "SCC"));
        var issue = await (await ac.PostAsJsonAsync("/api/v1/projects/scope-w/issues",
            new CreateIssueRequest("seed", "x"))).Content.ReadFromJsonAsync<IssueDto>();

        await using var ro = Factory("issue:read");
        using var roc = ro.CreateClient();

        var comment = await roc.PostAsJsonAsync($"/api/v1/projects/scope-w/issues/{issue!.Sequence}/comments",
            new CreateCommentRequest("hi"));
        comment.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var crit = await roc.PostAsJsonAsync($"/api/v1/projects/scope-w/issues/{issue.Sequence}/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("crit"));
        crit.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
