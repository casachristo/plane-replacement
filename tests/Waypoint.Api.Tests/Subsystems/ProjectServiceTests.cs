using FluentAssertions;
using Waypoint.Api.Subsystems.Projects.ProjectCrud;
using Waypoint.Contracts;
using Waypoint.Domain;
using Waypoint.Domain.Entities;
using Xunit;

namespace Waypoint.Api.Tests.Subsystems;

// Pure unit tests for the ProjectCrud Service: a fake Manager exercises mapping, the cairn-link
// trim/null rule, and cache invalidation — no DB, no HTTP.
public class ProjectServiceTests
{
    private sealed class FakeManager : IProjectManager
    {
        public Project? Tracked;
        public bool Saved;
        public string? InvalidatedSlug;
        public List<Project> All = new();

        public Task<Project> AddAsync(string slug, string name, string identifier, CancellationToken ct) =>
            Task.FromResult(new Project { Id = Guid.NewGuid(), Slug = slug, Name = name, Identifier = identifier });
        public Task<Project?> GetBySlugAsync(string slug, CancellationToken ct) =>
            Task.FromResult(All.FirstOrDefault(p => p.Slug == slug));
        public Task<Project?> GetTrackedBySlugAsync(string slug, CancellationToken ct) => Task.FromResult(Tracked);
        public Task<IReadOnlyList<Project>> ListAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Project>>(All);
        public Task SetDefaultStateAsync(Project project, Guid stateId, CancellationToken ct)
        { project.DefaultStateId = stateId; return Task.CompletedTask; }
        public Task SaveAsync(CancellationToken ct) { Saved = true; return Task.CompletedTask; }
        public void InvalidateSlugCache(string slug) => InvalidatedSlug = slug;
    }

    private static Project Proj(string slug, string name) =>
        new() { Id = Guid.NewGuid(), Slug = slug, Name = name, Identifier = slug.ToUpperInvariant() };

    [Fact]
    public async Task GetAsync_maps_present_project_and_returns_null_when_absent()
    {
        var fake = new FakeManager { All = { Proj("a", "Alpha") } };
        var svc = new ProjectService(fake);
        (await svc.GetAsync("a", CancellationToken.None))!.Name.Should().Be("Alpha");
        (await svc.GetAsync("missing", CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task ListAsync_maps_every_project_preserving_manager_order()
    {
        var fake = new FakeManager { All = { Proj("b", "Beta"), Proj("a", "Alpha") } };
        var dtos = await new ProjectService(fake).ListAsync(CancellationToken.None);
        dtos.Select(d => d.Name).Should().Equal("Beta", "Alpha");
    }

    [Fact]
    public async Task SetCairnLinkAsync_trims_the_name_saves_and_invalidates_the_slug_cache()
    {
        var fake = new FakeManager { Tracked = Proj("p", "P") };
        var dto = await new ProjectService(fake).SetCairnLinkAsync("p", new SetCairnLinkRequest("  Waypoint  "), CancellationToken.None);
        dto.CairnProjectName.Should().Be("Waypoint");
        fake.Tracked!.CairnProjectName.Should().Be("Waypoint");
        fake.Saved.Should().BeTrue();
        fake.InvalidatedSlug.Should().Be("p");
    }

    [Theory]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task SetCairnLinkAsync_clears_the_link_when_the_name_is_blank(string? name)
    {
        var fake = new FakeManager { Tracked = Proj("p", "P") };
        var dto = await new ProjectService(fake).SetCairnLinkAsync("p", new SetCairnLinkRequest(name), CancellationToken.None);
        dto.CairnProjectName.Should().BeNull();
        fake.Tracked!.CairnProjectName.Should().BeNull();
    }

    [Fact]
    public async Task SetCairnLinkAsync_throws_not_found_when_the_project_is_missing()
    {
        var fake = new FakeManager { Tracked = null };
        var act = () => new ProjectService(fake).SetCairnLinkAsync("nope", new SetCairnLinkRequest("X"), CancellationToken.None);
        (await act.Should().ThrowAsync<NotFoundException>()).Which.Code.Should().Be("project_not_found");
    }
}
