using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Waypoint.Domain;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<WaypointDbContext>
{
    public WaypointDbContext CreateDbContext(string[] args)
    {
        var connStr = Environment.GetEnvironmentVariable("WAYPOINT_DESIGN_TIME_CONNSTR")
                      ?? "Host=chris.box;Port=15432;Database=waypoint_design;Username=waypoint;Password=waypoint";
        var options = new DbContextOptionsBuilder<WaypointDbContext>()
            .UseNpgsql(connStr)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new WaypointDbContext(options);
    }
}
