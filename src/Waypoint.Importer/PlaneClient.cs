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

    public IAsyncEnumerable<JsonElement> ListProjectsAsync() =>
        PagedAsync($"workspaces/{_workspace}/projects/?per_page=100");

    public IAsyncEnumerable<JsonElement> ListIssuesAsync(string projectId) =>
        PagedAsync($"workspaces/{_workspace}/projects/{projectId}/issues/?per_page=100");

    public IAsyncEnumerable<JsonElement> ListStatesAsync(string projectId) =>
        PagedAsync($"workspaces/{_workspace}/projects/{projectId}/states/?per_page=100");

    public IAsyncEnumerable<JsonElement> ListLabelsAsync(string projectId) =>
        PagedAsync($"workspaces/{_workspace}/projects/{projectId}/labels/?per_page=100");

    public IAsyncEnumerable<JsonElement> ListCommentsAsync(string projectId, string issueId) =>
        PagedAsync($"workspaces/{_workspace}/projects/{projectId}/issues/{issueId}/comments/?per_page=100");

    public IAsyncEnumerable<JsonElement> ListActivitiesAsync(string projectId, string issueId) =>
        PagedAsync($"workspaces/{_workspace}/projects/{projectId}/issues/{issueId}/activities/?per_page=100");

    /// <summary>
    /// Handles Plane's paged response shape: {"results":[...], "next_cursor":"...", "next_page_results":bool, ...}.
    /// Retries on 429 with exponential backoff (Plane Community throttles aggressively —
    /// this is one of the original reasons we're replacing it).
    /// </summary>
    private async IAsyncEnumerable<JsonElement> PagedAsync(string url)
    {
        var next = url;
        while (next is not null)
        {
            var raw = await GetWithRetryAsync(next);
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (!root.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
            {
                // Defensive: some Plane endpoints (legacy or single-resource) may still return
                // a bare array. Treat it as a one-page response.
                if (root.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in root.EnumerateArray()) yield return item.Clone();
                }
                yield break;
            }
            foreach (var result in results.EnumerateArray()) yield return result.Clone();
            next = root.TryGetProperty("next_cursor", out var nc) && nc.ValueKind == JsonValueKind.String
                  && root.TryGetProperty("next_page_results", out var nr) && nr.GetBoolean()
                ? $"{url}&cursor={nc.GetString()}" : null;
        }
    }

    // Plane Community rate-limits aggressively. Throttle every call by this amount even
    // on a clean burst, and apply a minimum floor when honoring Retry-After (the header
    // values Plane sends are unrealistically short — it 429s again immediately).
    private static readonly TimeSpan PerCallDelay = TimeSpan.FromMilliseconds(1500);
    private static readonly TimeSpan MinRetryDelay = TimeSpan.FromSeconds(15);
    private DateTimeOffset _nextCallAt = DateTimeOffset.MinValue;

    private async Task<string> GetWithRetryAsync(string url)
    {
        // Proactive throttle: hold for PerCallDelay since the previous call.
        var wait = _nextCallAt - DateTimeOffset.UtcNow;
        if (wait > TimeSpan.Zero) await Task.Delay(wait);

        var delays = new[] { 15, 30, 60, 120, 180, 240, 300, 600 };   // seconds — total cap ~25 min
        for (var attempt = 0; ; attempt++)
        {
            using var resp = await _http.GetAsync(url);
            _nextCallAt = DateTimeOffset.UtcNow + PerCallDelay;

            if (resp.IsSuccessStatusCode) return await resp.Content.ReadAsStringAsync();
            if ((int)resp.StatusCode != 429 || attempt >= delays.Length) resp.EnsureSuccessStatusCode();

            // Honor Retry-After but enforce a minimum floor — Plane's suggested values are
            // too aggressive and immediately re-429.
            var delay = TimeSpan.FromSeconds(delays[attempt]);
            if (resp.Headers.RetryAfter is { } ra)
            {
                TimeSpan? hint = ra.Delta is { } d ? d : ra.Date is { } when ? when - DateTimeOffset.UtcNow : null;
                if (hint is { } h && h > delay) delay = h;
            }
            if (delay < MinRetryDelay) delay = MinRetryDelay;
            Console.Error.WriteLine($"  [429] backoff {delay.TotalSeconds:0}s — attempt {attempt + 1} for {url}");
            await Task.Delay(delay);
        }
    }

    public void Dispose() => _http.Dispose();
}
