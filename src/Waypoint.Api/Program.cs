using Microsoft.EntityFrameworkCore;
using Waypoint.Api.Middleware;
using Waypoint.Domain;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.Services.AddDbContext<WaypointDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")
                   ?? "Host=chris.box;Port=15432;Database=waypoint;Username=waypoint;Password=waypoint")
        .UseSnakeCaseNamingConvention());

var app = builder.Build();
if (app.Environment.IsDevelopment()) app.MapOpenApi();

app.UseMiddleware<RequestIdMiddleware>();
app.UseMiddleware<ErrorEnvelopeMiddleware>();

app.MapGet("/healthz/live", () => Results.Ok(new { status = "ok" }));

if (app.Environment.EnvironmentName == "Testing")
{
    app.MapGet("/__test_throws/not_found",
        IResult () => throw new NotFoundException("test_not_found", "probe"));
}

app.Run();

public partial class Program;
