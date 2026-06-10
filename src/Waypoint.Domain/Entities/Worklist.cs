using Waypoint.Domain.Enums;

namespace Waypoint.Domain.Entities;

/// <summary>WAY-17: an issue that was skipped during a batch run, with the agent's reason.</summary>
public sealed class WorklistSkip
{
    public Guid IssueId { get; set; }
    public string Reason { get; set; } = "";
}

/// <summary>
/// WAY-17: a project's built-in singleton batch queue. Exactly one row per project
/// (unique on ProjectId), auto-created with the project. Cairn's dispatcher drives it
/// through the internal surface — it is intentionally NOT shown on the Kanban board.
/// </summary>
public class Worklist
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public WorklistState State { get; set; } = WorklistState.Inactive;
    /// <summary>Issue ids in execution order (priority desc, sequence asc), rebuilt on start.</summary>
    public List<Guid> OrderedIds { get; set; } = new();
    /// <summary>0-based pointer into <see cref="OrderedIds"/>; the current item Cairn hands out.</summary>
    public int CurrentIdx { get; set; }
    public int DoneCount { get; set; }
    /// <summary>Issues skipped this run, each with the agent's reason.</summary>
    public List<WorklistSkip> Skipped { get; set; } = new();
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>The current issue id, or null when the queue is drained/empty.</summary>
    public Guid? CurrentId =>
        State == WorklistState.Active && CurrentIdx >= 0 && CurrentIdx < OrderedIds.Count
            ? OrderedIds[CurrentIdx]
            : null;

    public int RemainingCount => Math.Max(0, OrderedIds.Count - CurrentIdx);
    public int SkippedCount => Skipped.Count;
}
