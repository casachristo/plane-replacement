using System.Net.Http.Headers;
using System.Text.Json;

namespace Waypoint.Importer;

/// <summary>
/// Read-only client for the Plane API. Pages through endpoints and returns raw JsonElement
/// blocks for the dumper to write to disk verbatim. Mapping happens in the loader.
/// </summary>
public sealed class PlaneClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _workspace;

    public PlaneClient(string baseUrl, string apiKey, string workspace)
    {
        _http = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };
        _http.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _workspace = workspace;
    }

    public async IAsyncEnumerable<JsonElement> ListProjectsAsync()
    {
        await foreach (var page in PagedAsync($"workspaces/{_workspace}/projects/?per_page=100")) yield return page;
    }

    public async IAsyncEnumerable<JsonElement> ListIssuesAsync(string projectId)
    {
        await foreach (var page in PagedAsync($"workspaces/{_workspace}/projects/{projectId}/issues/?per_page=100"))
            yield return page;
    }

    public async IAsyncEnumerable<JsonElement> ListStatesAsync(string projectId)
    {
        var resp = await _http.GetStringAsync($"workspaces/{_workspace}/projects/{projectId}/states/");
        foreach (var s in JsonDocument.Parse(resp).RootElement.EnumerateArray()) yield return s.Clone();
    }

    public async IAsyncEnumerable<JsonElement> ListLabelsAsync(string projectId)
    {
        var resp = await _http.GetStringAsync($"workspaces/{_workspace}/projects/{projectId}/labels/");
        foreach (var s in JsonDocument.Parse(resp).RootElement.EnumerateArray()) yield return s.Clone();
    }

    public async IAsyncEnumerable<JsonElement> ListCommentsAsync(string projectId, string issueId)
    {
        var resp = await _http.GetStringAsync($"workspaces/{_workspace}/projects/{projectId}/issues/{issueId}/comments/");
        foreach (var s in JsonDocument.Parse(resp).RootElement.EnumerateArray()) yield return s.Clone();
    }

    public async IAsyncEnumerable<JsonElement> ListActivitiesAsync(string projectId, string issueId)
    {
        var resp = await _http.GetStringAsync($"workspaces/{_workspace}/projects/{projectId}/issues/{issueId}/activities/");
        foreach (var s in JsonDocument.Parse(resp).RootElement.EnumerateArray()) yield return s.Clone();
    }

    private async IAsyncEnumerable<JsonElement> PagedAsync(string url)
    {
        var next = url;
        while (next is not null)
        {
            var raw = await _http.GetStringAsync(next);
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            foreach (var result in root.GetProperty("results").EnumerateArray()) yield return result.Clone();
            next = root.TryGetProperty("next_cursor", out var nc) && nc.ValueKind == JsonValueKind.String && root.GetProperty("next_page_results").GetBoolean()
                ? $"{url}&cursor={nc.GetString()}" : null;
        }
    }

    public void Dispose() => _http.Dispose();
}
