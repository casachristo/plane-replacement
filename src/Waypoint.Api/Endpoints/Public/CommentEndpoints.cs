using Waypoint.Api.Auth;
using Waypoint.Api.Subsystems.Issues.Comments;
using Waypoint.Api.Subsystems.Issues.IssueCrud;
using Waypoint.Contracts;

namespace Waypoint.Api.Endpoints.PublicApi;

// Thin HTTP adapter: authenticate, resolve the issue via the Issues subsystem, delegate to
// ICommentService. No domain logic, no DbContext.
public static class CommentEndpoints
{
    public static void MapCommentEndpoints(this IEndpointRouteBuilder app, string projectsPrefix)
    {
        var group = app.MapGroup($"{projectsPrefix}/{{slug}}/issues/{{seq:int}}/comments");

        group.MapPost("/", async (string slug, int seq, CreateCommentRequest req,
            IIssueService issues, ICommentService comments, HttpContext ctx, CancellationToken ct) =>
        {
            var principal = AuthGuard.RequireWriteScope(ctx, "comment:create");
            var issue = await issues.ResolveAsync(slug, seq, ct);
            var dto = await comments.AddAsync(issue.Id, req.BodyMd, principal, ct);
            return Results.Created($"/api/v1/comments/{dto.Id}", dto);
        });

        group.MapGet("/", async (string slug, int seq,
            IIssueService issues, ICommentService comments, HttpContext ctx, CancellationToken ct) =>
        {
            AuthGuard.RequireAuth(ctx);
            var issue = await issues.ResolveAsync(slug, seq, ct);
            return Results.Ok(await comments.ListAsync(issue.Id, ct));
        });
    }
}
