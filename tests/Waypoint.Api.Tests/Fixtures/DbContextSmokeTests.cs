using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Waypoint.Domain;
using Xunit;

namespace Waypoint.Api.Tests.Fixtures;

public class DbContextSmokeTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;
    public DbContextSmokeTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task DbContext_can_connect_to_postgres()
    {
        var options = new DbContextOptionsBuilder<WaypointDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        await using var ctx = new WaypointDbContext(options);
        var canConnect = await ctx.Database.CanConnectAsync();
        canConnect.Should().BeTrue();
    }
}
