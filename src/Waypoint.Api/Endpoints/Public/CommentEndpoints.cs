using Microsoft.EntityFrameworkCore;
using Waypoint.Api.Auth;
using Waypoint.Api.Repositories;
using Waypoint.Contracts;
using Waypoint.Domain;

namespace Waypoint.Api.Endpoints.PublicApi;

public static class CommentEndpoints
{
    public static void MapCommentEndpoints(this IEndpointRouteBuilder app, string projectsPrefix)
    {
        var group = app.MapGroup($"{projectsPrefix}/{{slug}}/issues/{{seq:int}}/comments");

        group.MapPost("/", async (string slug, int seq, CreateCommentRequest req,
            IProjectRepository projects, ICommentRepository comments, WaypointDbContext db, HttpContext ctx, CancellationToken ct) =>
        {
            var principal = AuthGuard.RequireAuth(ctx);
            var project = await projects.GetBySlugAsync(slug, ct)
                ?? throw new NotFoundException("project_not_found", $"Project '{slug}' not found.");
            var issue = await db.Issues.FirstOrDefaultAsync(i => i.ProjectId == project.Id && i.SequenceId == seq, ct)
                ?? throw new NotFoundException("issue_not_found", $"Issue {project.Identifier}-{seq} not found.");
            var authorId = principal.Kind == PrincipalKind.Human && Guid.TryParse(principal.Id, out var uid) ? uid : (Guid?)null;
            var c = await comments.CreateAsync(issue.Id, req.BodyMd, authorUserId: authorId, ct);
            var dto = new CommentDto(c.Id, c.IssueId, c.BodyMd, c.AuthorUserId, c.CreatedAt, c.UpdatedAt);
            return Results.Created($"/api/v1/comments/{c.Id}", dto);
        });

        group.MapGet("/", async (string slug, int seq,
            IProjectRepository projects, ICommentRepository comments, WaypointDbContext db, HttpContext ctx, CancellationToken ct) =>
        {
            AuthGuard.RequireAuth(ctx);
            var project = await projects.GetBySlugAsync(slug, ct)
                ?? throw new NotFoundException("project_not_found", $"Project '{slug}' not found.");
            var issue = await db.Issues.FirstOrDefaultAsync(i => i.ProjectId == project.Id && i.SequenceId == seq, ct)
                ?? throw new NotFoundException("issue_not_found", $"Issue {project.Identifier}-{seq} not found.");
            var list = await comments.ListByIssueAsync(issue.Id, ct);
            return Results.Ok(list.Select(c =>
                new CommentDto(c.Id, c.IssueId, c.BodyMd, c.AuthorUserId, c.CreatedAt, c.UpdatedAt)));
        });
    }
}
