using FluentAssertions;
using Waypoint.Api.Subsystems.Projects.Labels;
using Waypoint.Api.Subsystems.Projects.ProjectCrud;
using Waypoint.Api.Subsystems.Projects.States;
using Waypoint.Contracts;
using Waypoint.Domain;
using Waypoint.Domain.Entities;
using Waypoint.Domain.Enums;
using Xunit;

namespace Waypoint.Api.Tests.Subsystems;

// Pure unit tests for the States and Labels feature Services: they resolve the project through
// the sibling IProjectService facade, then map the Manager's rows to DTOs. Fakes, no DB.
public class ProjectFeatureServiceTests
{
    // Minimal IProjectService double — only slug resolution is exercised by these features.
    private sealed class FakeProjects(Project? resolved) : IProjectService
    {
        public Task<Project?> GetBySlugAsync(string slug, CancellationToken ct) => Task.FromResult(resolved);
        public Task<IReadOnlyList<ProjectDto>> ListAsync(CancellationToken ct) => throw new NotImplementedException();
        public Task<ProjectDto?> GetAsync(string slug, CancellationToken ct) => throw new NotImplementedException();
        public Task<ProjectDto> SetCairnLinkAsync(string slug, SetCairnLinkRequest req, CancellationToken ct) => throw new NotImplementedException();
        public Task<Project> AddAsync(string slug, string name, string identifier, CancellationToken ct) => throw new NotImplementedException();
        public Task SetDefaultStateAsync(Project project, Guid stateId, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class FakeStateManager(IReadOnlyList<State> states) : IStateManager
    {
        public Task<Guid> SeedDefaultsAsync(Guid projectId, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<State>> ListByProjectAsync(Guid projectId, CancellationToken ct) => Task.FromResult(states);
    }

    private sealed class FakeLabelManager(IReadOnlyList<Label> labels) : ILabelManager
    {
        public Task<IReadOnlyList<Label>> ListByProjectAsync(Guid projectId, CancellationToken ct) => Task.FromResult(labels);
    }

    private static Project Proj => new() { Id = Guid.NewGuid(), Slug = "p", Name = "P", Identifier = "P" };

    [Fact]
    public async Task StateService_maps_each_state_field_to_the_dto()
    {
        var s = new State { Id = Guid.NewGuid(), Name = "In Progress", Group = StateGroup.Started, Color = "#3b82f6", SortOrder = 1, IsDefault = false };
        var svc = new StateService(new FakeProjects(Proj), new FakeStateManager(new[] { s }));
        var dto = (await svc.ListByProjectSlugAsync("p", CancellationToken.None)).Single();
        dto.Should().BeEquivalentTo(new StateDto(s.Id, "In Progress", "Started", "#3b82f6", 1, false));
    }

    [Fact]
    public async Task StateService_throws_not_found_when_the_project_is_missing()
    {
        var svc = new StateService(new FakeProjects(null), new FakeStateManager(Array.Empty<State>()));
        var act = () => svc.ListByProjectSlugAsync("missing", CancellationToken.None);
        (await act.Should().ThrowAsync<NotFoundException>()).Which.Code.Should().Be("project_not_found");
    }

    [Fact]
    public async Task LabelService_maps_each_label_field_to_the_dto()
    {
        var parent = Guid.NewGuid();
        var l = new Label { Id = Guid.NewGuid(), Name = "bug", Color = "#ef4444", ParentLabelId = parent };
        var svc = new LabelService(new FakeProjects(Proj), new FakeLabelManager(new[] { l }));
        var dto = (await svc.ListByProjectSlugAsync("p", CancellationToken.None)).Single();
        dto.Should().BeEquivalentTo(new LabelDto(l.Id, "bug", "#ef4444", parent));
    }

    [Fact]
    public async Task LabelService_throws_not_found_when_the_project_is_missing()
    {
        var svc = new LabelService(new FakeProjects(null), new FakeLabelManager(Array.Empty<Label>()));
        var act = () => svc.ListByProjectSlugAsync("missing", CancellationToken.None);
        (await act.Should().ThrowAsync<NotFoundException>()).Which.Code.Should().Be("project_not_found");
    }
}
