using Microsoft.EntityFrameworkCore;
using Waypoint.Api.Auth;
using Waypoint.Api.Pagination;
using Waypoint.Api.Repositories;
using Waypoint.Contracts;
using Waypoint.Domain;
using Waypoint.Domain.Entities;

namespace Waypoint.Api.Endpoints.PublicApi;

public static class IssueEndpoints
{
    public static void MapIssueEndpoints(this IEndpointRouteBuilder app, string projectsPrefix)
    {
        var group = app.MapGroup($"{projectsPrefix}/{{slug}}/issues");

        group.MapPost("/", async (string slug, CreateIssueRequest req,
            IProjectRepository projects, IIssueRepository issues, HttpContext ctx, CancellationToken ct) =>
        {
            AuthGuard.RequireAuth(ctx);
            var project = await projects.GetBySlugAsync(slug, ct)
                ?? throw new NotFoundException("project_not_found", $"Project '{slug}' not found.");
            var issue = await issues.CreateAsync(project.Id, req.Title, req.DescriptionMd, req.IssueTypeId, ct);
            var withIncludes = await issues.GetBySequenceAsync(project.Id, issue.SequenceId, ct);
            return Results.Created($"{projectsPrefix}/{slug}/issues/{issue.SequenceId}", ToDto(withIncludes!));
        });

        group.MapGet("/{seq:int}", async (string slug, int seq,
            IProjectRepository projects, IIssueRepository issues, HttpContext ctx, CancellationToken ct) =>
        {
            AuthGuard.RequireAuth(ctx);
            var project = await projects.GetBySlugAsync(slug, ct)
                ?? throw new NotFoundException("project_not_found", $"Project '{slug}' not found.");
            var issue = await issues.GetBySequenceAsync(project.Id, seq, ct)
                ?? throw new NotFoundException("issue_not_found", $"Issue {project.Identifier}-{seq} not found.");
            return Results.Ok(ToDto(issue));
        });

        group.MapPatch("/{seq:int}", async (string slug, int seq, UpdateIssueRequest req,
            IProjectRepository projects, IIssueRepository issues, HttpContext ctx, CancellationToken ct) =>
        {
            AuthGuard.RequireAuth(ctx);
            var project = await projects.GetBySlugAsync(slug, ct)
                ?? throw new NotFoundException("project_not_found", $"Project '{slug}' not found.");
            var updated = await issues.UpdateAsync(project.Id, seq, req.Title, req.DescriptionMd, req.Priority, ct);
            return Results.Ok(ToDto(updated));
        });

        group.MapPost("/{seq:int}/transitions", async (string slug, int seq, TransitionIssueRequest req,
            IProjectRepository projects, IIssueRepository issues, HttpContext ctx, CancellationToken ct) =>
        {
            AuthGuard.RequireAuth(ctx);
            var project = await projects.GetBySlugAsync(slug, ct)
                ?? throw new NotFoundException("project_not_found", $"Project '{slug}' not found.");
            var updated = await issues.TransitionAsync(project.Id, seq, req.ToStateId, ct);
            return Results.Ok(ToDto(updated));
        });

        group.MapGet("/", async (string slug, int? limit, string? cursor,
            IProjectRepository projects, IIssueRepository issues, HttpContext ctx, CancellationToken ct) =>
        {
            AuthGuard.RequireAuth(ctx);
            var project = await projects.GetBySlugAsync(slug, ct)
                ?? throw new NotFoundException("project_not_found", $"Project '{slug}' not found.");
            var pageSize = limit ?? 50;
            var (items, total) = await issues.ListAsync(project.Id, pageSize, cursor, ct);
            string? nextCursor = null;
            if (items.Count == pageSize)
            {
                var last = items[^1];
                nextCursor = Cursor.Encode(last.CreatedAt, last.Id);
            }
            var data = items.Select(ToDto).ToList();
            return Results.Ok(new PagedResponse<IssueDto>(data, nextCursor, total));
        });

        group.MapGet("/{seq:int}/activity", async (string slug, int seq,
            IProjectRepository projects, WaypointDbContext db, HttpContext ctx, CancellationToken ct) =>
        {
            AuthGuard.RequireAuth(ctx);
            var project = await projects.GetBySlugAsync(slug, ct)
                ?? throw new NotFoundException("project_not_found", $"Project '{slug}' not found.");
            var issue = await db.Issues.FirstOrDefaultAsync(i => i.ProjectId == project.Id && i.SequenceId == seq, ct)
                ?? throw new NotFoundException("issue_not_found", $"Issue {project.Identifier}-{seq} not found.");
            var events = await db.Activities.AsNoTracking()
                .Where(a => a.IssueId == issue.Id)
                .OrderBy(a => a.At)
                .Select(a => new ActivityDto(a.Id, a.ActorType.ToString(), a.ActorId, a.ActorLabel,
                    a.Verb, a.BeforeJson, a.AfterJson, a.At))
                .ToListAsync(ct);
            return Results.Ok(events);
        });
    }

    private static IssueDto ToDto(Issue i) => new(
        i.Id, i.SequenceId, i.Title, i.DescriptionMd,
        i.StateId, i.State.Name,
        i.IssueTypeId, i.IssueType.Name,
        (int)i.Priority,
        i.CreatedAt, i.UpdatedAt);
}
