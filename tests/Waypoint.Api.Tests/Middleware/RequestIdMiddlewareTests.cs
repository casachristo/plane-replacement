using FluentAssertions;
using Waypoint.Api.Tests.Fixtures;
using Xunit;

namespace Waypoint.Api.Tests.Middleware;

public class RequestIdMiddlewareTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public RequestIdMiddlewareTests(PostgresFixture pg) => _pg = pg;

    [Fact]
    public async Task Response_includes_X_Request_Id_when_request_omits_it()
    {
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/healthz/live");
        response.Headers.TryGetValues("X-Request-Id", out var values).Should().BeTrue();
        Guid.TryParse(values!.First(), out _).Should().BeTrue();
    }

    [Fact]
    public async Task Response_echoes_X_Request_Id_when_request_provides_one()
    {
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        var requestId = "test-req-abc-123";
        client.DefaultRequestHeaders.Add("X-Request-Id", requestId);
        var response = await client.GetAsync("/healthz/live");
        response.Headers.GetValues("X-Request-Id").First().Should().Be(requestId);
    }
}
