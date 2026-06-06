using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Waypoint.Api.Auth;
using Waypoint.Contracts;
using Waypoint.Domain;

namespace Waypoint.Api.Endpoints.PublicApi;

public sealed record SearchHit(
    Guid Id,
    string ProjectSlug,
    string ProjectIdentifier,
    int Sequence,
    string Title,
    string Snippet,
    float Rank);

public static class SearchEndpoints
{
    public static void MapSearchEndpoints(this IEndpointRouteBuilder app, string prefix)
    {
        app.MapGet($"{prefix}/search", async (string q, string? project, int? limit,
            WaypointDbContext db, HttpContext ctx, CancellationToken ct) =>
        {
            AuthGuard.RequireAuth(ctx);
            if (string.IsNullOrWhiteSpace(q))
                throw new ValidationException("query_required", "Provide a search query via ?q=...");

            var pageSize = Math.Clamp(limit ?? 25, 1, 100);

            // SqlQueryRaw with positional {0}/{1} params — EF binds them as NpgsqlParameters.
            // Column aliases MUST be snake_case: the DbContext uses UseSnakeCaseNamingConvention,
            // which rewrites the expected result-column names for SqlQueryRaw<SearchRow> to
            // snake_case (e.g. ProjectIdentifier -> project_identifier). PascalCase aliases here
            // make EF's materialization throw "required column 'project_identifier' not present".
            // pageSize is baked into the SQL as a literal because:
            //   (a) Postgres rejects LIMIT $N when it can't infer the parameter's integer type
            //       via the path SqlQuery uses (verified: search 500s when LIMIT is parameterized)
            //   (b) pageSize is already clamped 1..100 so no injection risk.
            // The {1}::text cast on the project filter is needed because Postgres won't evaluate
            // `$2 IS NULL` without a type hint when the parameter is sent as DBNull.
            var sql = @"
                SELECT
                    i.id AS ""id"",
                    p.slug AS ""project_slug"",
                    p.identifier AS ""project_identifier"",
                    i.sequence_id AS ""sequence"",
                    i.title AS ""title"",
                    ts_headline('english', coalesce(i.description_md, ''), plainto_tsquery('english', {0}),
                                'MaxFragments=2, MinWords=5, MaxWords=20, ShortWord=2') AS ""snippet"",
                    ts_rank_cd(i.search_vector, plainto_tsquery('english', {0}))::real AS ""rank""
                FROM issues i
                JOIN projects p ON p.id = i.project_id
                WHERE i.deleted_at IS NULL
                  AND p.deleted_at IS NULL
                  AND i.search_vector @@ plainto_tsquery('english', {0})
                  AND ({1}::text IS NULL OR p.slug = {1})
                ORDER BY ""rank"" DESC
                LIMIT " + pageSize.ToString(CultureInfo.InvariantCulture);

            var rows = await db.Database.SqlQueryRaw<SearchRow>(sql, q, (object?)project ?? DBNull.Value)
                .ToListAsync(ct);

            var hits = rows.Select(r => new SearchHit(r.Id, r.ProjectSlug, r.ProjectIdentifier,
                r.Sequence, r.Title, r.Snippet ?? "", r.Rank)).ToList();
            return Results.Ok(hits);
        });
    }

    private sealed record SearchRow(
        Guid Id,
        string ProjectSlug,
        string ProjectIdentifier,
        int Sequence,
        string Title,
        string? Snippet,
        float Rank);
}
