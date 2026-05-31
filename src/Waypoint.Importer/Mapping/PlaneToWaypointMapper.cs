using System.Text.Json;
using Waypoint.Domain.Entities;
using Waypoint.Domain.Enums;
using RmConverter = ReverseMarkdown.Converter;
using RmConfig = ReverseMarkdown.Config;

namespace Waypoint.Importer.Mapping;

/// <summary>
/// Maps Plane wire JSON to Waypoint entities. Pure: takes JsonElements, returns entities.
/// No DB access here — the loader handles persistence.
/// </summary>
public static class PlaneToWaypointMapper
{
    private static readonly RmConverter _md = new(new RmConfig
    {
        UnknownTags = RmConfig.UnknownTagsOption.PassThrough,
        GithubFlavored = true,
        RemoveComments = true,
        SmartHrefHandling = true,
    });

    public static Project MapProject(JsonElement p) => new()
    {
        Slug = p.GetProperty("identifier").GetString()!.ToLowerInvariant(),
        Name = p.GetProperty("name").GetString()!,
        Identifier = p.GetProperty("identifier").GetString()!,
    };

    public static State MapState(JsonElement s, Guid projectId) => new()
    {
        ProjectId = projectId,
        Name = s.GetProperty("name").GetString()!,
        Group = MapGroup(s.GetProperty("group").GetString()),
        Color = s.TryGetProperty("color", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString()! : "#94a3b8",
        SortOrder = s.TryGetProperty("sort_order", out var so) ? (int)so.GetDouble() : 0,
        IsDefault = s.TryGetProperty("default", out var d) && d.GetBoolean(),
    };

    public static Label MapLabel(JsonElement l, Guid projectId) => new()
    {
        ProjectId = projectId,
        Name = l.GetProperty("name").GetString()!,
        Color = l.TryGetProperty("color", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString()! : "#94a3b8",
    };

    public static Issue MapIssue(JsonElement i, Guid projectId, Guid stateId, Guid issueTypeId) => new()
    {
        ProjectId = projectId,
        SequenceId = i.GetProperty("sequence_id").GetInt32(),
        Title = i.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString()! : "(no title)",
        DescriptionMd = i.TryGetProperty("description_html", out var html) && html.ValueKind == JsonValueKind.String
            ? _md.Convert(html.GetString() ?? "") : "",
        StateId = stateId,
        IssueTypeId = issueTypeId,
        Priority = ParsePriority(i.TryGetProperty("priority", out var pr) && pr.ValueKind == JsonValueKind.String ? pr.GetString() : null),
        ExternalId = i.GetProperty("id").GetString(),
        ExternalSource = "plane",
    };

    public static Comment MapComment(JsonElement c, Guid issueId) => new()
    {
        IssueId = issueId,
        BodyMd = c.TryGetProperty("comment_html", out var html) && html.ValueKind == JsonValueKind.String
            ? _md.Convert(html.GetString() ?? "")
            : (c.TryGetProperty("comment_stripped", out var stripped) && stripped.ValueKind == JsonValueKind.String ? stripped.GetString()! : ""),
    };

    public static Activity MapActivity(JsonElement a, Guid issueId)
    {
        var verb = a.TryGetProperty("verb", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        var field = a.TryGetProperty("field", out var f) && f.ValueKind == JsonValueKind.String ? f.GetString() : null;
        return new Activity
        {
            IssueId = issueId,
            ActorType = ActorType.System,
            Verb = $"legacy_plane_{verb ?? "unknown"}{(field is not null ? "_" + field : "")}",
            BeforeJson = a.TryGetProperty("old_value", out var ov) && ov.ValueKind != JsonValueKind.Null ? ov.ToString() : null,
            AfterJson = a.TryGetProperty("new_value", out var nv) && nv.ValueKind != JsonValueKind.Null ? nv.ToString() : null,
        };
    }

    /// <summary>Detects label hacks: name starts with "type:", "epic:" → returns (kind, stripped-name).</summary>
    public static (string Kind, string Name) ClassifyLabel(string labelName)
    {
        if (labelName.StartsWith("type:", StringComparison.OrdinalIgnoreCase))
            return ("issue_type", labelName[5..].Trim());
        if (labelName.StartsWith("epic:", StringComparison.OrdinalIgnoreCase))
            return ("epic", labelName[5..].Trim());
        return ("label", labelName);
    }

    private static StateGroup MapGroup(string? g) => g?.ToLowerInvariant() switch
    {
        "backlog" => StateGroup.Backlog,
        "unstarted" => StateGroup.Unstarted,
        "started" => StateGroup.Started,
        "completed" => StateGroup.Completed,
        "cancelled" => StateGroup.Cancelled,
        _ => StateGroup.Backlog,
    };

    private static Priority ParsePriority(string? p) => p?.ToLowerInvariant() switch
    {
        "urgent" => Priority.Urgent,
        "high" => Priority.High,
        "medium" => Priority.Medium,
        "low" => Priority.Low,
        _ => Priority.None,
    };
}
