using Waypoint.Domain;
using Waypoint.Domain.Entities;

namespace Waypoint.Api.Subsystems.Projects.IssueTypes;

// Manager — owns IssueType rows. Seeds a project's default "Task" type, bound to the project's
// default workflow. The only thing that persists IssueType; private to the IssueTypes feature.
public interface IIssueTypeManager
{
    Task SeedDefaultAsync(Guid projectId, Guid defaultWorkflowId, CancellationToken ct);
}

public sealed class IssueTypeManager(WaypointDbContext db) : IIssueTypeManager
{
    public async Task SeedDefaultAsync(Guid projectId, Guid defaultWorkflowId, CancellationToken ct)
    {
        var issueType = new IssueType
        {
            ProjectId = projectId, Name = "Task", IsDefault = true, DefaultWorkflowId = defaultWorkflowId,
        };
        db.IssueTypes.Add(issueType);
        await db.SaveChangesAsync(ct);
    }
}
