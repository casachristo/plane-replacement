using Microsoft.EntityFrameworkCore;
using Waypoint.Domain.Entities;

namespace Waypoint.Domain;

public class WaypointDbContext(DbContextOptions<WaypointDbContext> options) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<State> States => Set<State>();
    public DbSet<IssueType> IssueTypes => Set<IssueType>();
    public DbSet<Workflow> Workflows => Set<Workflow>();
    public DbSet<WorkflowTransition> WorkflowTransitions => Set<WorkflowTransition>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasPostgresExtension("pgcrypto");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WaypointDbContext).Assembly);
    }
}
