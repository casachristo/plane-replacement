using Waypoint.Api.Repositories;
using Waypoint.Contracts;
using Waypoint.Domain;

namespace Waypoint.Api.Endpoints;

public static class ProjectEndpoints
{
    public static void MapProjectEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/projects");

        group.MapPost("/", async (CreateProjectRequest req, IProjectRepository repo, CancellationToken ct) =>
        {
            var p = await repo.CreateAsync(req.Slug, req.Name, req.Identifier, ct);
            var dto = new ProjectDto(p.Id, p.Slug, p.Name, p.Identifier, p.CreatedAt, p.UpdatedAt);
            return Results.Created($"/api/v1/projects/{p.Slug}", dto);
        });

        group.MapGet("/{slug}", async (string slug, IProjectRepository repo, CancellationToken ct) =>
        {
            var p = await repo.GetBySlugAsync(slug, ct)
                ?? throw new NotFoundException("project_not_found", $"Project '{slug}' not found.");
            return Results.Ok(new ProjectDto(p.Id, p.Slug, p.Name, p.Identifier, p.CreatedAt, p.UpdatedAt));
        });
    }
}
