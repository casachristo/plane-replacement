using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Xunit;

namespace Waypoint.Api.Tests.Middleware;

public class ErrorEnvelopeMiddlewareTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public ErrorEnvelopeMiddlewareTests(PostgresFixture pg) => _pg = pg;

    [Fact]
    public async Task NotFoundException_maps_to_404_with_envelope()
    {
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/__test_throws/not_found");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        body!.Error.Code.Should().Be("test_not_found");
        body.RequestId.Should().NotBeNullOrEmpty();
    }
}
