using Microsoft.EntityFrameworkCore;
using Waypoint.Api;
using Waypoint.Api.Auth;
using Waypoint.Api.Endpoints.InternalApi;
using Waypoint.Api.Endpoints.PublicApi;
using Waypoint.Api.Middleware;
using Waypoint.Api.Repositories;
using Waypoint.Domain;

if (args.Length > 0 && args[0] == "seed-token")
{
    var hostBuilder = WebApplication.CreateBuilder();
    hostBuilder.Services.AddDbContext<WaypointDbContext>(opts =>
        opts.UseNpgsql(hostBuilder.Configuration.GetConnectionString("Postgres")
                       ?? "Host=chris.box;Port=15432;Database=waypoint;Username=waypoint;Password=waypoint")
            .UseSnakeCaseNamingConvention());
    var seedApp = hostBuilder.Build();
    Environment.Exit(await SeedToken.RunAsync(args[1..], seedApp.Services));
}

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseKestrel(opts =>
{
    opts.ListenAnyIP(8080);  // public
    opts.ListenAnyIP(8081);  // internal
});

builder.Services.AddOpenApi();
builder.Services.AddDbContext<WaypointDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")
                   ?? "Host=chris.box;Port=15432;Database=waypoint;Username=waypoint;Password=waypoint")
        .UseSnakeCaseNamingConvention());

builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<IIssueRepository, IssueRepository>();
builder.Services.AddScoped<ICommentRepository, CommentRepository>();
builder.Services.AddScoped<IIntentRepository, IntentRepository>();
builder.Services.AddScoped<IPrincipalResolver, ServiceBearerResolver>();

var app = builder.Build();
if (app.Environment.IsDevelopment()) app.MapOpenApi();

app.UseMiddleware<RequestIdMiddleware>();
app.UseMiddleware<ErrorEnvelopeMiddleware>();
app.UseMiddleware<SurfaceGuardMiddleware>();
app.UseMiddleware<IdempotencyMiddleware>();
app.UseMiddleware<PrincipalMiddleware>();
app.UseMiddleware<AuditLogMiddleware>();

app.MapGet("/healthz/live", () => Results.Ok(new { status = "ok" }));

// Public surface (:8080 in K8s; locally both ports route same code)
app.MapProjectEndpoints("/api/v1/projects");
app.MapIssueEndpoints("/api/v1/projects");
app.MapCommentEndpoints("/api/v1/projects");

// Internal surface (:8081)
app.MapProjectEndpoints("/internal/v1/projects");
app.MapIssueEndpoints("/internal/v1/projects");
app.MapCommentEndpoints("/internal/v1/projects");
app.MapIntentEndpoints();

if (app.Environment.EnvironmentName == "Testing")
{
    app.MapGet("/__test_throws/not_found",
        IResult () => throw new NotFoundException("test_not_found", "probe"));
}

app.Run();

public partial class Program;
