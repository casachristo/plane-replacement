using Microsoft.EntityFrameworkCore;
using Waypoint.Api.Auth;
using Waypoint.Contracts;
using Waypoint.Domain;
using Waypoint.Domain.Entities;

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

            // Use Postgres plainto_tsquery so users can type natural prose without operator
            // syntax. ts_rank_cd gives weighted ranking; ts_headline produces an excerpt with
            // <b> tags around matches (we strip those client-side or render as needed).
            var pageSize = Math.Clamp(limit ?? 25, 1, 100);
            var rows = await db.Database.SqlQuery<SearchRow>($"""
                SELECT
                    i.id AS "Id",
                    p.slug AS "ProjectSlug",
                    p.identifier AS "ProjectIdentifier",
                    i.sequence_id AS "Sequence",
                    i.title AS "Title",
                    ts_headline('english', coalesce(i.description_md, ''), plainto_tsquery('english', {q}),
                                'MaxFragments=2, MinWords=5, MaxWords=20, ShortWord=2') AS "Snippet",
                    ts_rank_cd(i.search_vector, plainto_tsquery('english', {q}))::real AS "Rank"
                FROM issues i
                JOIN projects p ON p.id = i.project_id
                WHERE i.deleted_at IS NULL
                  AND p.deleted_at IS NULL
                  AND i.search_vector @@ plainto_tsquery('english', {q})
                  AND ({project}::text IS NULL OR p.slug = {project})
                ORDER BY "Rank" DESC
                LIMIT {pageSize}
                """).ToListAsync(ct);

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
