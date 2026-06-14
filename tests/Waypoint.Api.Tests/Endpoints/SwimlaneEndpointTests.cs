using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Waypoint.Api.Auth;
using Waypoint.Api.Cairn;
using Waypoint.Api.Repositories;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

// WAY-15 (Phase 1): project<->Cairn link + module-swimlane endpoint. Unlinked projects keep
// the single-row Kanban behaviour; a linked project surfaces module rows from the Cairn source.
public class SwimlaneEndpointTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public SwimlaneEndpointTests(PostgresFixture pg) => _pg = pg;

    private sealed class FakeModuleSource : ICairnModuleSource
    {
        public Task<IReadOnlyList<string>> GetModulesAsync(string n, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>(new[] { "Domain", "API" });
    }

    private WaypointApiFactory Factory(bool withFakeModules = false, params string[] scopes) => new()
    {
        PostgresConnectionString = _pg.ConnectionString,
        TestPrincipal = new Principal(PrincipalKind.Human, System.Guid.NewGuid().ToString(), "u",
            scopes.Length == 0 ? new[] { "admin" } : scopes),
        ConfigureTestServices = withFakeModules
            ? services => services.AddSingleton<ICairnModuleSource, FakeModuleSource>()
            : null,
    };

    [Fact]
    public async Task Unlinked_project_reports_no_swimlanes()
    {
        await using var f = Factory();
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        await c.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("sw-a", "A", "SWA"));

        var s = await (await c.GetAsync("/api/v1/projects/sw-a/swimlanes")).Content.ReadFromJsonAsync<SwimlanesDto>();
        s!.CairnLinked.Should().BeFalse();
        s.Modules.Should().BeEmpty();
    }

    [Fact]
    public async Task Linking_a_project_surfaces_modules_from_the_cairn_source()
    {
        await using var f = Factory(withFakeModules: true);
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        await c.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("sw-b", "B", "SWB"));

        var link = await c.PutAsJsonAsync("/api/v1/projects/sw-b/cairn-link", new SetCairnLinkRequest("Waypoint"));
        link.StatusCode.Should().Be(HttpStatusCode.OK);
        (await link.Content.ReadFromJsonAsync<ProjectDto>())!.CairnProjectName.Should().Be("Waypoint");

        var s = await (await c.GetAsync("/api/v1/projects/sw-b/swimlanes")).Content.ReadFromJsonAsync<SwimlanesDto>();
        s!.CairnLinked.Should().BeTrue();
        s.Modules.Should().Equal("Domain", "API");
    }

    [Fact]
    public async Task Clearing_the_link_returns_to_single_row_behaviour()
    {
        await using var f = Factory(withFakeModules: true);
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        await c.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("sw-d", "D", "SWD"));
        await c.PutAsJsonAsync("/api/v1/projects/sw-d/cairn-link", new SetCairnLinkRequest("Waypoint"));

        await c.PutAsJsonAsync("/api/v1/projects/sw-d/cairn-link", new SetCairnLinkRequest(null));
        var s = await (await c.GetAsync("/api/v1/projects/sw-d/swimlanes")).Content.ReadFromJsonAsync<SwimlanesDto>();
        s!.CairnLinked.Should().BeFalse();
    }

    [Fact]
    public async Task Non_admin_cannot_set_the_cairn_link()
    {
        await using var f = Factory(scopes: new[] { "issue:read" });
        await f.EnsureMigratedAsync();
        using (var scope = f.Services.CreateScope())
        {
            var projects = scope.ServiceProvider.GetRequiredService<Waypoint.Api.Subsystems.Projects.IProjectsOrchestrator>();
            await projects.ProvisionAsync(new CreateProjectRequest("sw-c", "C", "SWC"), CancellationToken.None);
        }
        using var c = f.CreateClient();
        var resp = await c.PutAsJsonAsync("/api/v1/projects/sw-c/cairn-link", new SetCairnLinkRequest("Waypoint"));
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);  // RequireScope("admin")
    }
}
