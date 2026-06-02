using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Waypoint.Api.Auth;
using Waypoint.Domain;

namespace Waypoint.Api.Tests.Fixtures;

public class WaypointApiFactory : WebApplicationFactory<Program>
{
    public required string PostgresConnectionString { get; init; }

    private static readonly Guid TestUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    /// <summary>If set, the test pipeline returns this principal for every request, bypassing
    /// real OIDC/bearer auth. Default: a human with admin scope, sufficient for most tests.
    /// The Id matches a seeded users row (see EnsureMigratedAsync) so FKs from comments,
    /// activities, etc. resolve.</summary>
    public Principal TestPrincipal { get; init; } = new Principal(
        Kind: PrincipalKind.Human,
        Id: TestUserId.ToString(),
        DisplayName: "Test User",
        Scopes: ["issue:read", "issue:create", "issue:transition", "comment:create", "admin"]);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<WaypointDbContext>));
            if (descriptor is not null) services.Remove(descriptor);

            services.AddDbContext<WaypointDbContext>(opts =>
                opts.UseNpgsql(PostgresConnectionString).UseSnakeCaseNamingConvention());

            // Drop the production resolvers and inject a fixed test principal so endpoints'
            // RequireAuth checks pass without exercising OIDC or bearer-token paths.
            var resolverDescriptors = services.Where(d => d.ServiceType == typeof(IPrincipalResolver)).ToList();
            foreach (var d in resolverDescriptors) services.Remove(d);
            services.AddScoped<IPrincipalResolver>(_ => new FixedPrincipalResolver(TestPrincipal));
        });
    }

    public async Task EnsureMigratedAsync()
    {
        using var scope = Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<WaypointDbContext>();
        await ctx.Database.MigrateAsync();
        // Seed the test user that TestPrincipal points at, so author FKs hold.
        if (!await ctx.Users.IgnoreQueryFilters().AnyAsync(u => u.Id == TestUserId))
        {
            ctx.Users.Add(new Waypoint.Domain.Entities.User
            {
                Id = TestUserId,
                Email = "test@waypoint.local",
                DisplayName = "Test User",
            });
            await ctx.SaveChangesAsync();
        }
    }
}

internal sealed class FixedPrincipalResolver(Principal principal) : IPrincipalResolver
{
    public Task<Principal?> ResolveAsync(HttpContext ctx, CancellationToken ct) => Task.FromResult<Principal?>(principal);
}
