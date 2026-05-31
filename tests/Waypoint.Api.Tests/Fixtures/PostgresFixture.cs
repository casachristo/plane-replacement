using Npgsql;
using Xunit;

namespace Waypoint.Api.Tests.Fixtures;

/// <summary>
/// Per-test-class isolated Postgres database, hosted on the homelab test Postgres
/// (chris.box:15432 by default). Override with WAYPOINT_TEST_PG_HOST / _PORT /
/// _USER / _PASSWORD env vars.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private static readonly string MasterHost = Environment.GetEnvironmentVariable("WAYPOINT_TEST_PG_HOST") ?? "chris.box";
    private static readonly string MasterPort = Environment.GetEnvironmentVariable("WAYPOINT_TEST_PG_PORT") ?? "15432";
    private static readonly string MasterUser = Environment.GetEnvironmentVariable("WAYPOINT_TEST_PG_USER") ?? "waypoint";
    private static readonly string MasterPassword = Environment.GetEnvironmentVariable("WAYPOINT_TEST_PG_PASSWORD") ?? "waypoint";

    private readonly string _dbName = $"waypoint_test_{Guid.NewGuid():N}";

    public string ConnectionString =>
        $"Host={MasterHost};Port={MasterPort};Database={_dbName};Username={MasterUser};Password={MasterPassword};Include Error Detail=true";

    private static string MasterConnectionString =>
        $"Host={MasterHost};Port={MasterPort};Database=postgres;Username={MasterUser};Password={MasterPassword}";

    public async Task InitializeAsync()
    {
        await using var conn = new NpgsqlConnection(MasterConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand($"CREATE DATABASE \"{_dbName}\"", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        try
        {
            await using var conn = new NpgsqlConnection(MasterConnectionString);
            await conn.OpenAsync();
            // Drop with FORCE to kick any lingering connections (Postgres 13+).
            await using var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_dbName}\" WITH (FORCE)", conn);
            await cmd.ExecuteNonQueryAsync();
        }
        catch
        {
            // Best-effort cleanup; lingering test DBs can be reaped manually.
        }
    }
}
