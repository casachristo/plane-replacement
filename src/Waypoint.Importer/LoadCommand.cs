using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Waypoint.Domain;
using Waypoint.Domain.Entities;
using Waypoint.Domain.Enums;
using Waypoint.Importer.Mapping;

namespace Waypoint.Importer;

public static class LoadCommand
{
    public static async Task<int> RunAsync(string dumpDir, string connStr, bool dryRun)
    {
        var report = new LoadReport();
        var options = new DbContextOptionsBuilder<WaypointDbContext>()
            .UseNpgsql(connStr).UseSnakeCaseNamingConvention().Options;

        await using var db = new WaypointDbContext(options);
        await db.Database.MigrateAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        var projectsJson = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(dumpDir, "projects.json"))).RootElement;
        foreach (var pJson in projectsJson.EnumerateArray())
        {
            var planeProjectId = pJson.GetProperty("id").GetString()!;
            var project = PlaneToWaypointMapper.MapProject(pJson);
            db.Projects.Add(project);
            await db.SaveChangesAsync();
            report.Projects++;

            var defaultState = new State
            {
                ProjectId = project.Id, Name = "Backlog", Group = StateGroup.Backlog,
                Color = "#94a3b8", SortOrder = 0, IsDefault = true,
            };
            db.States.Add(defaultState);
            await db.SaveChangesAsync();
            project.DefaultStateId = defaultState.Id;

            var defaultType = new IssueType
            {
                ProjectId = project.Id, Name = "Task", IsDefault = true,
            };
            db.IssueTypes.Add(defaultType);
            await db.SaveChangesAsync();

            var projDir = Path.Combine(dumpDir, "projects", planeProjectId);
            if (!Directory.Exists(projDir)) continue;

            // States — preserve Plane state ids → Waypoint state ids
            var stateMap = new Dictionary<string, Guid>();
            var statesJson = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(projDir, "states.json"))).RootElement;
            foreach (var s in statesJson.EnumerateArray())
            {
                var state = PlaneToWaypointMapper.MapState(s, project.Id);
                if (state.Name == "Backlog" && state.IsDefault) { stateMap[s.GetProperty("id").GetString()!] = defaultState.Id; continue; }
                db.States.Add(state);
                await db.SaveChangesAsync();
                stateMap[s.GetProperty("id").GetString()!] = state.Id;
                report.States++;
            }

            // Labels (classify: type / epic / label)
            var labelMap = new Dictionary<string, Guid>();
            var typeMap = new Dictionary<string, Guid> { [""] = defaultType.Id };
            if (File.Exists(Path.Combine(projDir, "labels.json")))
            {
                var labelsJson = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(projDir, "labels.json"))).RootElement;
                foreach (var l in labelsJson.EnumerateArray())
                {
                    var planeId = l.GetProperty("id").GetString()!;
                    var (kind, name) = PlaneToWaypointMapper.ClassifyLabel(l.GetProperty("name").GetString() ?? "");
                    if (kind == "issue_type")
                    {
                        var existing = await db.IssueTypes.FirstOrDefaultAsync(t => t.ProjectId == project.Id && t.Name == name);
                        if (existing is null)
                        {
                            existing = new IssueType { ProjectId = project.Id, Name = name };
                            db.IssueTypes.Add(existing);
                            await db.SaveChangesAsync();
                            report.IssueTypes++;
                        }
                        typeMap[planeId] = existing.Id;
                    }
                    else if (kind == "epic")
                    {
                        var epic = new Epic
                        {
                            ProjectId = project.Id, Title = name, SequenceId = report.EpicsByProject(project.Id) + 1,
                        };
                        db.Epics.Add(epic);
                        await db.SaveChangesAsync();
                        report.Epics++;
                    }
                    else
                    {
                        var label = PlaneToWaypointMapper.MapLabel(l, project.Id);
                        db.Labels.Add(label);
                        await db.SaveChangesAsync();
                        labelMap[planeId] = label.Id;
                        report.Labels++;
                    }
                }
            }

            // Issues
            var issuesJson = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(projDir, "issues.json"))).RootElement;
            foreach (var iJson in issuesJson.EnumerateArray())
            {
                var planeStateId = iJson.GetProperty("state").GetString();
                if (planeStateId is null || !stateMap.TryGetValue(planeStateId, out var stateId)) stateId = defaultState.Id;
                var issue = PlaneToWaypointMapper.MapIssue(iJson, project.Id, stateId, defaultType.Id);
                db.Issues.Add(issue);
                await db.SaveChangesAsync();
                report.Issues++;

                var planeIssueId = iJson.GetProperty("id").GetString()!;
                var issueDir = Path.Combine(projDir, "issues", planeIssueId);
                if (!Directory.Exists(issueDir)) continue;

                if (File.Exists(Path.Combine(issueDir, "comments.json")))
                {
                    var commentsJson = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(issueDir, "comments.json"))).RootElement;
                    foreach (var c in commentsJson.EnumerateArray())
                    {
                        db.Comments.Add(PlaneToWaypointMapper.MapComment(c, issue.Id));
                        report.Comments++;
                    }
                    await db.SaveChangesAsync();
                }

                if (File.Exists(Path.Combine(issueDir, "activities.json")))
                {
                    var activitiesJson = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(issueDir, "activities.json"))).RootElement;
                    foreach (var a in activitiesJson.EnumerateArray())
                    {
                        db.Activities.Add(PlaneToWaypointMapper.MapActivity(a, issue.Id));
                        report.Activities++;
                    }
                    await db.SaveChangesAsync();
                }
            }
        }

        if (dryRun) { await tx.RollbackAsync(); Console.Error.WriteLine("[DRY-RUN] rolled back."); }
        else { await tx.CommitAsync(); }

        Console.WriteLine(report.ToString());
        await File.WriteAllTextAsync(Path.Combine(dumpDir, "_load_report.json"),
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
}

public sealed class LoadReport
{
    public int Projects { get; set; }
    public int States { get; set; }
    public int IssueTypes { get; set; }
    public int Epics { get; set; }
    public int Labels { get; set; }
    public int Issues { get; set; }
    public int Comments { get; set; }
    public int Activities { get; set; }
    private readonly Dictionary<Guid, int> _epicsByProject = new();

    public int EpicsByProject(Guid projectId)
    {
        _epicsByProject.TryGetValue(projectId, out var n);
        _epicsByProject[projectId] = n + 1;
        return n;
    }

    public override string ToString() =>
        $"Projects={Projects} States={States} IssueTypes={IssueTypes} Epics={Epics} " +
        $"Labels={Labels} Issues={Issues} Comments={Comments} Activities={Activities}";
}
