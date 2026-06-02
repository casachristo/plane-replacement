using Waypoint.Api.Auth;
using Waypoint.Api.Repositories;
using Waypoint.Domain;

namespace Waypoint.Api.Endpoints.InternalApi;

public sealed record FileIntentRequest(string ModulePath, string IntentText);
public sealed record FileIntentResponse(Guid IntentId);

public static class IntentEndpoints
{
    public static void MapIntentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/internal/v1/projects/{slug}/intents");

        group.MapPost("/", async (string slug, FileIntentRequest req,
            IProjectRepository projects, IIntentRepository intents,
            HttpContext ctx, CancellationToken ct) =>
        {
            var principal = AuthGuard.RequireAuth(ctx);
            if (principal.Kind != PrincipalKind.InternalService)
                throw new ValidationException("internal_service_required", "Intent endpoints accept service tokens only.");
            var project = await projects.GetBySlugAsync(slug, ct)
                ?? throw new NotFoundException("project_not_found", $"Project '{slug}' not found.");
            if (!Guid.TryParse(principal.Id, out var tokenId))
                throw new ValidationException("invalid_principal", "Internal principal must have a token id.");

            var intent = await intents.FileAsync(project.Id, req.ModulePath, req.IntentText, tokenId, ct);
            return Results.Ok(new FileIntentResponse(intent.Id));
        });

        // DELETE with linked_issue_seq as query param — DELETE bodies don't infer in minimal APIs.
        app.MapDelete("/internal/v1/intents/{intentId:guid}",
            async (Guid intentId, int? linkedIssueSeq,
                IIntentRepository intents, HttpContext ctx, CancellationToken ct) =>
            {
                var principal = AuthGuard.RequireAuth(ctx);
                if (principal.Kind != PrincipalKind.InternalService)
                    throw new ValidationException("internal_service_required", "Intent endpoints accept service tokens only.");
                await intents.ReleaseAsync(intentId, linkedIssueSeq, ct);
                return Results.NoContent();
            });
    }
}
