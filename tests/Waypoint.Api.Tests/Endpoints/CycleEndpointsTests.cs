using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Waypoint.Domain;
using Waypoint.Domain.Entities;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

public class CycleEndpointsTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public CycleEndpointsTests(PostgresFixture pg) => _pg = pg;

    [Fact]
    public async Task GET_cycles_lists_project_cycles_in_start_date_order_with_mapped_fields()
    {
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("cyc-proj", "Cyc", "CYC"));

        var early = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var late = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        Guid lateId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WaypointDbContext>();
            var project = db.Projects.Single(p => p.Slug == "cyc-proj");
            // Insert out of order to prove the endpoint orders by StartDate.
            var b = new Cycle { ProjectId = project.Id, Name = "Sprint 2", StartDate = late, EndDate = late.AddDays(14), State = "upcoming" };
            db.Add(new Cycle { ProjectId = project.Id, Name = "Sprint 1", StartDate = early, EndDate = early.AddDays(14), State = "active" });
            db.Add(b);
            db.SaveChanges();
            lateId = b.Id;
        }

        var resp = await client.GetAsync("/api/v1/projects/cyc-proj/cycles");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var cycles = await resp.Content.ReadFromJsonAsync<List<CycleDto>>();
        cycles!.Select(c => c.Name).Should().Equal("Sprint 1", "Sprint 2");
        var second = cycles![1];
        second.Should().BeEquivalentTo(new CycleDto(lateId, "Sprint 2", late, late.AddDays(14), "upcoming"));
    }

    [Fact]
    public async Task GET_cycles_is_empty_for_a_new_project()
    {
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("cyc-empty", "Empty", "CYE"));

        var resp = await client.GetAsync("/api/v1/projects/cyc-empty/cycles");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resp.Content.ReadFromJsonAsync<List<CycleDto>>())!.Should().BeEmpty();
    }

    [Fact]
    public async Task GET_cycles_returns_404_for_missing_slug()
    {
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();

        var resp = await client.GetAsync("/api/v1/projects/no-such-proj/cycles");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await resp.Content.ReadFromJsonAsync<ErrorResponse>())!.Error.Code.Should().Be("project_not_found");
    }
}
