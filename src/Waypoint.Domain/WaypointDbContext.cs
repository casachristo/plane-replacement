using Microsoft.EntityFrameworkCore;

namespace Waypoint.Domain;

public class WaypointDbContext(DbContextOptions<WaypointDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WaypointDbContext).Assembly);
    }
}
