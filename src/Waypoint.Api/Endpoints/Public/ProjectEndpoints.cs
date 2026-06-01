using Waypoint.Api.Repositories;
using Waypoint.Contracts;
using Waypoint.Domain;

namespace Waypoint.Api.Endpoints.PublicApi;

public static class ProjectEndpoints
{
    /// <summary>Maps project routes under <paramref name="prefix"/>. Called once with /api/v1/projects (public) and once with /internal/v1/projects (internal).</summary>
    public static void MapProjectEndpoints(this IEndpointRouteBuilder app, string prefix)
    {
        var group = app.MapGroup(prefix);

        group.MapPost("/", async (CreateProjectRequest req, IProjectRepository repo, CancellationToken ct) =>
        {
            var p = await repo.CreateAsync(req.Slug, req.Name, req.Identifier, ct);
            var dto = new ProjectDto(p.Id, p.Slug, p.Name, p.Identifier, p.CreatedAt, p.UpdatedAt);
            return Results.Created($"{prefix}/{p.Slug}", dto);
        });

        group.MapGet("/", async (IProjectRepository repo, CancellationToken ct) =>
        {
            var list = await repo.ListAsync(ct);
            return Results.Ok(list.Select(p => new ProjectDto(p.Id, p.Slug, p.Name, p.Identifier, p.CreatedAt, p.UpdatedAt)));
        });

        group.MapGet("/{slug}", async (string slug, IProjectRepository repo, CancellationToken ct) =>
        {
            var p = await repo.GetBySlugAsync(slug, ct)
                ?? throw new NotFoundException("project_not_found", $"Project '{slug}' not found.");
            return Results.Ok(new ProjectDto(p.Id, p.Slug, p.Name, p.Identifier, p.CreatedAt, p.UpdatedAt));
        });
    }
}
