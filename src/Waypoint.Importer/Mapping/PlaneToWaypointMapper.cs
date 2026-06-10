using System.Text.Json;
using System.Text.RegularExpressions;
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
            // GetRawText() preserves valid JSON shape — strings come back quoted ("Done").
            // JsonElement.ToString() on a string returns the bare value, which Postgres
            // rejects when inserting into jsonb columns.
            BeforeJson = AsJsonOrNull(a, "old_value"),
            AfterJson = AsJsonOrNull(a, "new_value"),
        };
    }

    private static string? AsJsonOrNull(JsonElement parent, string property)
    {
        if (!parent.TryGetProperty(property, out var prop) || prop.ValueKind == JsonValueKind.Null) return null;
        return prop.GetRawText();
    }

    // WAY-7: a Plane ticket carries its acceptance criteria as Markdown checkbox lines
    // (`- [ ]` / `- [x]`). Opt-in at import time, these become first-class AcceptanceCriterion
    // rows so the gate (WAY-4) and UI can reason about them structurally instead of re-parsing
    // Markdown forever. The match is line-anchored and accepts -, * or + bullets.
    private static readonly Regex CheckboxLine = new(
        @"^\s*[-*+]\s+\[(?<mark>[ xX])\]\s+(?<text>.+?)\s*$",
        RegexOptions.Compiled);

    /// <summary>
    /// Parses Markdown task-list checkboxes out of a description into ordered AcceptanceCriterion
    /// entities (IssueId left unset for the caller to assign). Non-checkbox lines are ignored.
    /// Pure — no DB, no clock.
    /// </summary>
    public static List<AcceptanceCriterion> ParseAcceptanceCriteria(string? descriptionMd)
    {
        var result = new List<AcceptanceCriterion>();
        if (string.IsNullOrEmpty(descriptionMd)) return result;

        var position = 0;
        foreach (var line in descriptionMd.Split('\n'))
        {
            var m = CheckboxLine.Match(line);
            if (!m.Success) continue;
            result.Add(new AcceptanceCriterion
            {
                Position = ++position,
                Text = m.Groups["text"].Value,
                Checked = m.Groups["mark"].Value is "x" or "X",
            });
        }
        return result;
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
