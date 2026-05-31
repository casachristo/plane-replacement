using Microsoft.EntityFrameworkCore;
using Waypoint.Api.Endpoints;
using Waypoint.Api.Middleware;
using Waypoint.Api.Repositories;
using Waypoint.Domain;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.Services.AddDbContext<WaypointDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")
                   ?? "Host=chris.box;Port=15432;Database=waypoint;Username=waypoint;Password=waypoint")
        .UseSnakeCaseNamingConvention());
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<IIssueRepository, IssueRepository>();
builder.Services.AddScoped<ICommentRepository, CommentRepository>();

var app = builder.Build();
if (app.Environment.IsDevelopment()) app.MapOpenApi();

app.UseMiddleware<RequestIdMiddleware>();
app.UseMiddleware<ErrorEnvelopeMiddleware>();
app.UseMiddleware<IdempotencyMiddleware>();

app.MapGet("/healthz/live", () => Results.Ok(new { status = "ok" }));
app.MapProjectEndpoints();
app.MapIssueEndpoints();
app.MapCommentEndpoints();

if (app.Environment.EnvironmentName == "Testing")
{
    app.MapGet("/__test_throws/not_found",
        IResult () => throw new NotFoundException("test_not_found", "probe"));
}

app.Run();

public partial class Program;
