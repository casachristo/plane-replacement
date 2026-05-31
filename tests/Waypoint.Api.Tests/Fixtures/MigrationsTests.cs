using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Waypoint.Domain;
using Xunit;

namespace Waypoint.Api.Tests.Fixtures;

public class MigrationsTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;
    public MigrationsTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrations_apply_cleanly_and_create_project_shell_tables()
    {
        var options = new DbContextOptionsBuilder<WaypointDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        await using (var ctx = new WaypointDbContext(options))
        {
            await ctx.Database.MigrateAsync();
        }

        var expected = new[] { "projects", "states", "issue_types", "workflows", "workflow_transitions" };
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' ORDER BY table_name", conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        var found = new List<string>();
        while (await reader.ReadAsync()) found.Add(reader.GetString(0));
        found.Should().Contain(expected);
    }

    [Fact]
    public async Task Project_shell_has_expected_unique_indexes()
    {
        var options = new DbContextOptionsBuilder<WaypointDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        await using (var ctx = new WaypointDbContext(options))
        {
            await ctx.Database.MigrateAsync();
        }
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
            SELECT count(*) FROM pg_indexes
            WHERE schemaname = 'public'
              AND (
                (tablename = 'projects' AND indexname LIKE '%slug%') OR
                (tablename = 'projects' AND indexname LIKE '%identifier%') OR
                (tablename = 'workflow_transitions')
              )", conn);
        var result = (long?)await cmd.ExecuteScalarAsync();
        result.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task Migrations_create_content_tables()
    {
        var options = new DbContextOptionsBuilder<WaypointDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        await using (var ctx = new WaypointDbContext(options))
        {
            await ctx.Database.MigrateAsync();
        }

        var expected = new[] { "users", "issues", "comments", "activity" };
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' ORDER BY table_name", conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        var found = new List<string>();
        while (await reader.ReadAsync()) found.Add(reader.GetString(0));
        found.Should().Contain(expected);
    }

    [Fact]
    public async Task Issues_have_unique_index_on_project_and_sequence()
    {
        var options = new DbContextOptionsBuilder<WaypointDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        await using (var ctx = new WaypointDbContext(options))
        {
            await ctx.Database.MigrateAsync();
        }
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
            SELECT count(*) FROM pg_indexes
            WHERE tablename = 'issues'
              AND indexdef LIKE '%project_id%sequence_id%'", conn);
        var result = (long?)await cmd.ExecuteScalarAsync();
        result.Should().BeGreaterThanOrEqualTo(1);
    }
}
