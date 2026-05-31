using System.Text.Json;

namespace Waypoint.Importer;

public static class DumpCommand
{
    public static async Task<int> RunAsync(string baseUrl, string apiKey, string workspace, string outDir)
    {
        Directory.CreateDirectory(outDir);
        using var client = new PlaneClient(baseUrl, apiKey, workspace);
        var manifest = new Dictionary<string, object> { ["timestamp"] = DateTimeOffset.UtcNow };

        var projectsList = new List<JsonElement>();
        await foreach (var p in client.ListProjectsAsync()) projectsList.Add(p);
        await File.WriteAllTextAsync(Path.Combine(outDir, "projects.json"),
            JsonSerializer.Serialize(projectsList, new JsonSerializerOptions { WriteIndented = true }));

        manifest["projects"] = projectsList.Count;
        Console.Error.WriteLine($"Dumped {projectsList.Count} projects");

        var perProject = new Dictionary<string, int>();
        foreach (var p in projectsList)
        {
            var projectId = p.GetProperty("id").GetString()!;
            var projectName = p.GetProperty("name").GetString()!;
            var projDir = Path.Combine(outDir, "projects", projectId);
            Directory.CreateDirectory(projDir);

            await WriteAsync(projDir, "states", client.ListStatesAsync(projectId));
            await WriteAsync(projDir, "labels", client.ListLabelsAsync(projectId));

            var issues = new List<JsonElement>();
            await foreach (var i in client.ListIssuesAsync(projectId)) issues.Add(i);
            await File.WriteAllTextAsync(Path.Combine(projDir, "issues.json"),
                JsonSerializer.Serialize(issues, new JsonSerializerOptions { WriteIndented = true }));
            perProject[projectName] = issues.Count;
            Console.Error.WriteLine($"  {projectName}: {issues.Count} issues");

            foreach (var i in issues)
            {
                var issueId = i.GetProperty("id").GetString()!;
                var issueDir = Path.Combine(projDir, "issues", issueId);
                Directory.CreateDirectory(issueDir);
                await WriteAsync(issueDir, "comments", client.ListCommentsAsync(projectId, issueId));
                await WriteAsync(issueDir, "activities", client.ListActivitiesAsync(projectId, issueId));
            }
        }

        manifest["per_project_issue_counts"] = perProject;
        await File.WriteAllTextAsync(Path.Combine(outDir, "_dump_manifest.json"),
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
        Console.Error.WriteLine($"Dump complete. Manifest at {outDir}/_dump_manifest.json");
        return 0;
    }

    private static async Task WriteAsync(string dir, string name, IAsyncEnumerable<JsonElement> items)
    {
        var list = new List<JsonElement>();
        await foreach (var item in items) list.Add(item);
        await File.WriteAllTextAsync(Path.Combine(dir, $"{name}.json"),
            JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
    }
}
