using FluentAssertions;
using Npgsql;
using Xunit;

namespace Waypoint.Api.Tests.Fixtures;

public class PostgresFixtureSmokeTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public PostgresFixtureSmokeTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Fixture_provides_a_reachable_postgres()
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT 1", conn);
        var result = await cmd.ExecuteScalarAsync();
        result.Should().Be(1);
    }
}
