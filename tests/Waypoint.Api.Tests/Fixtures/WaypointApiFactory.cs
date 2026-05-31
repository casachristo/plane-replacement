using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Waypoint.Domain;

namespace Waypoint.Api.Tests.Fixtures;

public class WaypointApiFactory : WebApplicationFactory<Program>
{
    public required string PostgresConnectionString { get; init; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<WaypointDbContext>));
            if (descriptor is not null) services.Remove(descriptor);

            services.AddDbContext<WaypointDbContext>(opts =>
                opts.UseNpgsql(PostgresConnectionString).UseSnakeCaseNamingConvention());
        });
    }

    public async Task EnsureMigratedAsync()
    {
        using var scope = Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<WaypointDbContext>();
        await ctx.Database.MigrateAsync();
    }
}
