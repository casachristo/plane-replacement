using Microsoft.EntityFrameworkCore;
using Waypoint.Api.Auth;
using Waypoint.Api.Repositories;
using Waypoint.Api.Subsystems.Issues.Comments;
using Waypoint.Contracts;
using Waypoint.Domain;
using Waypoint.Domain.Entities;

namespace Waypoint.Api.Endpoints.PublicApi;

// Thin HTTP adapter (the API layer is exempt from the subsystem pattern): authenticate,
// resolve the issue, and delegate to ICommentService. No domain logic here.
public static class CommentEndpoints
{
    public static void MapCommentEndpoints(this IEndpointRouteBuilder app, string projectsPrefix)
    {
        var group = app.MapGroup($"{projectsPrefix}/{{slug}}/issues/{{seq:int}}/comments");

        group.MapPost("/", async (string slug, int seq, CreateCommentRequest req,
            IProjectRepository projects, ICommentService comments, WaypointDbContext db, HttpContext ctx, CancellationToken ct) =>
        {
            var principal = AuthGuard.RequireWriteScope(ctx, "comment:create");
            var issue = await ResolveIssueAsync(projects, db, slug, seq, ct);
            var dto = await comments.AddAsync(issue.Id, req.BodyMd, principal, ct);
            return Results.Created($"/api/v1/comments/{dto.Id}", dto);
        });

        group.MapGet("/", async (string slug, int seq,
            IProjectRepository projects, ICommentService comments, WaypointDbContext db, HttpContext ctx, CancellationToken ct) =>
        {
            AuthGuard.RequireAuth(ctx);
            var issue = await ResolveIssueAsync(projects, db, slug, seq, ct);
            return Results.Ok(await comments.ListAsync(issue.Id, ct));
        });
    }

    // Issue resolution is the Issues subsystem's concern; it moves into IssueService when that
    // subsystem is refactored (WAY-41). Kept inline so the Comments cut stays bounded.
    private static async Task<Issue> ResolveIssueAsync(
        IProjectRepository projects, WaypointDbContext db, string slug, int seq, CancellationToken ct)
    {
        var project = await projects.GetBySlugAsync(slug, ct)
            ?? throw new NotFoundException("project_not_found", $"Project '{slug}' not found.");
        return await db.Issues.FirstOrDefaultAsync(i => i.ProjectId == project.Id && i.SequenceId == seq, ct)
            ?? throw new NotFoundException("issue_not_found", $"Issue {project.Identifier}-{seq} not found.");
    }
}
