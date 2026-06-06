using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using System.Text;
using Waypoint.Api.Endpoints.PublicApi;
using Waypoint.Api.Middleware;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

public class TargetedKills2MutationCoverage : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public TargetedKills2MutationCoverage(PostgresFixture pg) => _pg = pg;

    [Fact]
    public async Task Issue_GET_AC_inline_orders_by_Position_ascending()
    {
        await using var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        await c.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("tk2a", "p", "TK2A"));
        await c.PostAsJsonAsync("/api/v1/projects/tk2a/issues", new CreateIssueRequest("t", "b"));
        // Insert AC out of order: 3, 1, 2.
        await c.PostAsJsonAsync("/api/v1/projects/tk2a/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("third", Position: 3));
        await c.PostAsJsonAsync("/api/v1/projects/tk2a/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("first", Position: 1));
        await c.PostAsJsonAsync("/api/v1/projects/tk2a/issues/1/acceptance-criteria",
            new CreateAcceptanceCriterionRequest("second", Position: 2));

        // GET on Issue must return AC in Position-ascending order (kills OrderBy→OrderByDescending mutation).
        var issue = await (await c.GetAsync("/api/v1/projects/tk2a/issues/1"))
            .Content.ReadFromJsonAsync<IssueDto>();
        issue!.AcceptanceCriteria.Should().HaveCount(3);
        issue.AcceptanceCriteria.Select(a => a.Text).Should().Equal("first", "second", "third");
    }

    [Fact]
    public async Task Project_GET_states_orders_by_SortOrder_ascending()
    {
        await using var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        await c.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("tk2b", "p", "TK2B"));

        var states = await (await c.GetAsync("/api/v1/projects/tk2b/states"))
            .Content.ReadFromJsonAsync<List<StateDto>>();
        states.Should().NotBeNullOrEmpty();
        // Kills OrderBy→OrderByDescending: SortOrder must be in ASCENDING order.
        states!.Should().BeInAscendingOrder(s => s.SortOrder);
    }

    [Fact]
    public async Task Project_GET_states_only_returns_THIS_projects_states()
    {
        // Kills: s.ProjectId != project.Id equality mutation.
        await using var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        await c.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("tk2c1", "P1", "TK2C1"));
        await c.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("tk2c2", "P2", "TK2C2"));

        var states1 = await (await c.GetAsync("/api/v1/projects/tk2c1/states"))
            .Content.ReadFromJsonAsync<List<StateDto>>();
        var states2 = await (await c.GetAsync("/api/v1/projects/tk2c2/states"))
            .Content.ReadFromJsonAsync<List<StateDto>>();

        states1!.Select(s => s.Id).Should().NotIntersectWith(states2!.Select(s => s.Id));
    }

    [Fact]
    public async Task Idempotency_cache_hit_DOES_serve_within_TTL()
    {
        // Kills the cached.ExpiresAt > DateTimeOffset.UtcNow equality mutations:
        // if flipped to ExpiresAt < UtcNow, every cache check would miss.
        var key = $"ttl-{Guid.NewGuid()}";
        var middleware = new IdempotencyMiddleware(async c =>
        {
            c.Response.StatusCode = 200;
            c.Response.ContentType = "application/json";
            await c.Response.Body.WriteAsync(Encoding.UTF8.GetBytes("first"));
        });
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "POST";
        ctx.Request.Headers[IdempotencyMiddleware.HeaderName] = key;
        ctx.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(ctx);

        var nextCalled = false;
        var middleware2 = new IdempotencyMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var ctx2 = new DefaultHttpContext();
        ctx2.Request.Method = "POST";
        ctx2.Request.Headers[IdempotencyMiddleware.HeaderName] = key;
        ctx2.Response.Body = new MemoryStream();
        await middleware2.InvokeAsync(ctx2);
        nextCalled.Should().BeFalse();   // cache hit must short-circuit
    }
}
