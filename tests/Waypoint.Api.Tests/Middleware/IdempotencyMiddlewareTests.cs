using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Waypoint.Api.Auth;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Xunit;

namespace Waypoint.Api.Tests.Middleware;

public class IdempotencyMiddlewareTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public IdempotencyMiddlewareTests(PostgresFixture pg) => _pg = pg;

    [Fact]
    public async Task Repeating_POST_with_same_Idempotency_Key_returns_first_response()
    {
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        var key = Guid.NewGuid().ToString();
        client.DefaultRequestHeaders.Add("Idempotency-Key", key);

        var first = await client.PostAsJsonAsync("/api/v1/projects",
            new CreateProjectRequest("idem-proj", "I", "ID1"));
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstBody = await first.Content.ReadFromJsonAsync<ProjectDto>();

        var second = await client.PostAsJsonAsync("/api/v1/projects",
            new CreateProjectRequest("idem-proj-different", "I2", "ID2"));
        second.StatusCode.Should().Be(HttpStatusCode.Created);
        var secondBody = await second.Content.ReadFromJsonAsync<ProjectDto>();
        secondBody!.Id.Should().Be(firstBody!.Id);
    }

    private static Principal Caller(string id) => new(
        PrincipalKind.Human, id, id,
        ["issue:read", "issue:create", "issue:transition", "comment:create", "admin"]);

    [Fact]
    public async Task Same_key_different_principals_do_not_share_a_cached_response()
    {
        // WAY-26 regression: the cache was keyed on the header alone, so a second caller
        // reusing the same Idempotency-Key received the first caller cached body. The static
        // cache is shared across factory instances, so two factories with different principals
        // exercise it directly.
        var key = Guid.NewGuid().ToString();
        await using var fa = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString, TestPrincipal = Caller(Guid.NewGuid().ToString()) };
        await fa.EnsureMigratedAsync();
        using var ca = fa.CreateClient();
        ca.DefaultRequestHeaders.Add("Idempotency-Key", key);

        await using var fb = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString, TestPrincipal = Caller(Guid.NewGuid().ToString()) };
        using var cb = fb.CreateClient();
        cb.DefaultRequestHeaders.Add("Idempotency-Key", key);

        var ra = await ca.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("idem-pa", "A", "IPA"));
        var bodyA = await ra.Content.ReadFromJsonAsync<ProjectDto>();
        var rb = await cb.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("idem-pb", "B", "IPB"));
        rb.StatusCode.Should().Be(HttpStatusCode.Created);
        var bodyB = await rb.Content.ReadFromJsonAsync<ProjectDto>();

        bodyB!.Id.Should().NotBe(bodyA!.Id);   // B did NOT receive the cached response of A
        bodyB.Slug.Should().Be("idem-pb");
    }

    [Fact]
    public async Task Same_key_same_principal_different_path_do_not_collide()
    {
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        var key = Guid.NewGuid().ToString();
        client.DefaultRequestHeaders.Add("Idempotency-Key", key);

        await client.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("idem-path", "P", "IPP"));
        // Same key, DIFFERENT path: must create a real issue, not replay the project response.
        var issueResp = await client.PostAsJsonAsync("/api/v1/projects/idem-path/issues", new CreateIssueRequest("real issue", "body"));
        issueResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var issue = await issueResp.Content.ReadFromJsonAsync<IssueDto>();
        issue!.Title.Should().Be("real issue");
    }
}
