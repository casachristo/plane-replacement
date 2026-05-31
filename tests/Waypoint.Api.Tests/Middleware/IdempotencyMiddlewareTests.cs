using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
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
}
