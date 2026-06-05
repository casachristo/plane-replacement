using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth.Claims;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Trace;
using Waypoint.Api;
using Waypoint.Api.Auth;
using Waypoint.Api.Endpoints.InternalApi;
using Waypoint.Api.Endpoints.PublicApi;
using Waypoint.Api.Middleware;
using Waypoint.Api.Repositories;
using Waypoint.Domain;

if (args.Length > 0 && (args[0] == "seed-token" || args[0] == "migrate"))
{
    var hostBuilder = WebApplication.CreateBuilder();
    hostBuilder.Services.AddDbContext<WaypointDbContext>(opts =>
        opts.UseNpgsql(hostBuilder.Configuration.GetConnectionString("Postgres")
                       ?? "Host=chris.box;Port=15432;Database=waypoint;Username=waypoint;Password=waypoint")
            .UseSnakeCaseNamingConvention());
    var seedApp = hostBuilder.Build();
    if (args[0] == "migrate")
    {
        using var scope = seedApp.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WaypointDbContext>();
        await db.Database.MigrateAsync();
        Console.WriteLine("Migrations applied.");
        Environment.Exit(0);
    }
    Environment.Exit(await SeedToken.RunAsync(args[1..], seedApp.Services));
}

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseKestrel(opts =>
{
    opts.ListenAnyIP(8080);
    opts.ListenAnyIP(8081);
});

builder.Services.Configure<OidcOptions>(builder.Configuration.GetSection(OidcOptions.SectionName));

builder.Services.AddOpenApi();
builder.Services.AddDbContext<WaypointDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")
                   ?? "Host=chris.box;Port=15432;Database=waypoint;Username=waypoint;Password=waypoint")
        .UseSnakeCaseNamingConvention());

builder.Services.AddMemoryCache();
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<Waypoint.Api.Webhooks.IWebhookPublisher, Waypoint.Api.Webhooks.WebhookPublisher>();
builder.Services.AddScoped<IIssueRepository, IssueRepository>();
builder.Services.AddScoped<ICommentRepository, CommentRepository>();
builder.Services.AddScoped<IIntentRepository, IntentRepository>();
builder.Services.AddScoped<IPrincipalResolver, OidcSessionResolver>();
builder.Services.AddScoped<IPrincipalResolver, ServiceBearerResolver>();
builder.Services.AddHttpClient("waypoint-webhooks", client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddHostedService<Waypoint.Api.Webhooks.WebhookDispatcher>();

// OpenTelemetry — traces only (metrics already exposed via WaypointMetrics + Prometheus).
// OTLP exporter ships to whatever the OTEL_EXPORTER_OTLP_ENDPOINT env var points at;
// when unset, the exporter is a no-op (no warning spam, just zero export).
builder.Services.AddOpenTelemetry().WithTracing(t => t
    .AddSource("Waypoint.Api")
    .AddAspNetCoreInstrumentation()
    .AddHttpClientInstrumentation()
    .AddEntityFrameworkCoreInstrumentation()
    .AddOtlpExporter()
);

// OIDC + Cookie authentication: used only for the /auth/* login flow. Once we have a
// waypoint_session cookie, the OidcSessionResolver takes over and we no longer rely on
// ASP.NET Core's auth pipeline for application requests.
var oidcSection = builder.Configuration.GetSection(OidcOptions.SectionName);
var authority = oidcSection.GetValue<string>("Authority") ?? "https://auth.chris.box";
var clientId = oidcSection.GetValue<string>("ClientId") ?? "waypoint";
var clientSecret = oidcSection.GetValue<string>("ClientSecret") ?? "";
builder.Services.AddAuthentication(opts =>
{
    opts.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    opts.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie()
.AddOpenIdConnect(opts =>
{
    opts.Authority = authority;
    opts.ClientId = clientId;
    opts.ClientSecret = clientSecret;
    opts.ResponseType = "code";
    opts.UsePkce = true;
    opts.SaveTokens = true;
    // Authelia's ID token doesn't include email/groups by default; fetch them from
    // the /userinfo endpoint and merge into the ClaimsPrincipal.
    opts.GetClaimsFromUserInfoEndpoint = true;
    opts.ClaimActions.MapJsonKey("email", "email");
    opts.ClaimActions.MapJsonKey("name", "name");
    opts.ClaimActions.MapJsonKey("groups", "groups");
    // .NET 10 defaults to Pushed Authorization Requests (PAR). Authelia's per-client
    // PAR support is opt-in; we use the classic redirect flow.
    opts.PushedAuthorizationBehavior = Microsoft.AspNetCore.Authentication.OpenIdConnect.PushedAuthorizationBehavior.Disable;
    // Match the redirect_uri registered in Authelia (see deploy/helm + authelia.yaml).
    opts.CallbackPath = "/auth/callback";
    opts.Scope.Clear();
    foreach (var s in oidcSection.GetValue<string[]>("Scopes") ?? ["openid", "profile", "email", "groups"])
        opts.Scope.Add(s);
    opts.TokenValidationParameters = new TokenValidationParameters
    {
        NameClaimType = oidcSection.GetValue<string>("NameClaim") ?? "name",
    };
});

// Trust X-Forwarded-* from Traefik so OIDC builds https:// redirect URIs even though
// the pod sees HTTP internally.
builder.Services.Configure<ForwardedHeadersOptions>(opts =>
{
    opts.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                          | ForwardedHeaders.XForwardedProto
                          | ForwardedHeaders.XForwardedHost;
    opts.KnownIPNetworks.Clear();
    opts.KnownProxies.Clear();
    // Trust the K3s pod CIDR so X-Forwarded-* from Traefik is actually applied.
    // Clearing without re-adding (the prior bug) silently dropped all forwarded
    // headers, leaving Request.IsHttps=false inside the pod.
    opts.KnownIPNetworks.Add(new System.Net.IPNetwork(System.Net.IPAddress.Parse("10.42.0.0"), 16));
    opts.KnownIPNetworks.Add(new System.Net.IPNetwork(System.Net.IPAddress.Parse("10.43.0.0"), 16));
});

var app = builder.Build();
app.UseForwardedHeaders();
if (app.Environment.IsDevelopment()) app.MapOpenApi();

// Auto-migrate on startup. The migration Job in Helm is belt-and-suspenders,
// but the startup migration is what lets a fresh `helm install` succeed without
// the Job racing the postgres StatefulSet.
using (var startupScope = app.Services.CreateScope())
{
    var db = startupScope.ServiceProvider.GetRequiredService<WaypointDbContext>();
    var attempts = 0;
    while (true)
    {
        try { await db.Database.MigrateAsync(); break; }
        catch (Npgsql.NpgsqlException) when (++attempts < 30)
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
    }
}

app.UseAuthentication();
app.UseMiddleware<RequestIdMiddleware>();
app.UseMiddleware<ErrorEnvelopeMiddleware>();
app.UseMiddleware<SurfaceGuardMiddleware>();
app.UseMiddleware<IdempotencyMiddleware>();
app.UseMiddleware<PrincipalMiddleware>();
app.UseMiddleware<AuditLogMiddleware>();

app.MapGet("/healthz/live", () => Results.Ok(new { status = "ok" }));
// Readiness: the pod can't serve requests if Postgres is unreachable, so this is the
// one K8s should target for `readinessProbe`. /healthz/live stays cheap for liveness.
app.MapGet("/healthz/ready", async (WaypointDbContext db, CancellationToken ct) =>
{
    try
    {
        return await db.Database.CanConnectAsync(ct)
            ? Results.Ok(new { status = "ready" })
            : Results.Json(new { status = "db_unreachable" }, statusCode: 503);
    }
    catch (Exception ex)
    {
        return Results.Json(new { status = "db_error", message = ex.GetType().Name }, statusCode: 503);
    }
});

app.MapAuthEndpoints();
app.MapWebhookEndpoints();
app.MapAdminEndpoints();
app.MapSearchEndpoints("/api/v1");
app.MapSearchEndpoints("/internal/v1");

// Public surface (:8080)
app.MapProjectEndpoints("/api/v1/projects");
app.MapIssueEndpoints("/api/v1/projects");
app.MapCommentEndpoints("/api/v1/projects");
app.MapAcceptanceCriterionEndpoints("/api/v1/projects");

// Internal surface (:8081)
app.MapProjectEndpoints("/internal/v1/projects");
app.MapIssueEndpoints("/internal/v1/projects");
app.MapCommentEndpoints("/internal/v1/projects");
app.MapAcceptanceCriterionEndpoints("/internal/v1/projects");
app.MapIntentEndpoints();

if (app.Environment.EnvironmentName == "Testing")
{
    app.MapGet("/__test_throws/not_found",
        IResult () => throw new NotFoundException("test_not_found", "probe"));
}

app.Run();

public partial class Program;
