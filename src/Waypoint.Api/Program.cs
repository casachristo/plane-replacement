var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();

var app = builder.Build();
if (app.Environment.IsDevelopment()) app.MapOpenApi();

app.MapGet("/healthz/live", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program; // for WebApplicationFactory<Program> in tests
