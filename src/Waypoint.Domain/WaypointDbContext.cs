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
    public DbSet<User> Users => Set<User>();
    public DbSet<Issue> Issues => Set<Issue>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<ApiToken> ApiTokens => Set<ApiToken>();
    public DbSet<TokenAuditLog> TokenAuditLog => Set<TokenAuditLog>();
    public DbSet<IssueIntent> IssueIntents => Set<IssueIntent>();
    public DbSet<Epic> Epics => Set<Epic>();
    public DbSet<Cycle> Cycles => Set<Cycle>();
    public DbSet<Label> Labels => Set<Label>();
    public DbSet<IssueLabel> IssueLabels => Set<IssueLabel>();
    public DbSet<Component> Components => Set<Component>();
    public DbSet<IssueComponent> IssueComponents => Set<IssueComponent>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<WebhookSubscription> WebhookSubscriptions => Set<WebhookSubscription>();
    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasPostgresExtension("pgcrypto");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WaypointDbContext).Assembly);
    }
}
