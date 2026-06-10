using System.Text.Json;
using FluentAssertions;
using Waypoint.Domain.Enums;
using Waypoint.Importer.Mapping;
using Xunit;

namespace Waypoint.Domain.Tests.Importer;

/// <summary>
/// WAY-21: the importer seeds a To Do (Unstarted) default — never a Backlog default — and
/// preserves a source backlog-group state as a non-default Icebox state on re-import.
/// </summary>
public class ImporterStateSeedTests
{
    private static readonly Guid Proj = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public void DefaultSeedState_is_a_ToDo_Unstarted_default()
    {
        var s = PlaneToWaypointMapper.DefaultSeedState(Proj);
        s.Name.Should().Be("To Do");
        s.Group.Should().Be(StateGroup.Unstarted);
        s.IsDefault.Should().BeTrue();
        s.SortOrder.Should().Be(0);
        s.ProjectId.Should().Be(Proj);
    }

    [Fact]
    public void DefaultSeedState_is_never_a_Backlog_state()
    {
        var s = PlaneToWaypointMapper.DefaultSeedState(Proj);
        s.Name.Should().NotBe("Backlog");
        s.Group.Should().NotBe(StateGroup.Backlog);
    }

    [Fact]
    public void MapState_preserves_a_source_backlog_group_as_Icebox()
    {
        // A Plane backlog-group state keeps StateGroup.Backlog (Icebox/on-ice) when imported.
        var json = JsonDocument.Parse(
            """{"name":"Backlog","group":"backlog","color":"#abcdef","sort_order":3,"default":true}""")
            .RootElement;

        var state = PlaneToWaypointMapper.MapState(json, Proj);

        state.Name.Should().Be("Backlog");
        state.Group.Should().Be(StateGroup.Backlog);
        // The loader forces imported states to non-default; the mapper just reflects the source
        // flag, which the loader overrides — assert the group/name preservation here.
        state.Color.Should().Be("#abcdef");
        state.SortOrder.Should().Be(3);
    }

    [Theory]
    [InlineData("unstarted", StateGroup.Unstarted)]
    [InlineData("started", StateGroup.Started)]
    [InlineData("completed", StateGroup.Completed)]
    [InlineData("cancelled", StateGroup.Cancelled)]
    public void MapState_maps_each_source_group(string group, StateGroup expected)
    {
        var json = JsonDocument.Parse(
            $$"""{"name":"S","group":"{{group}}","color":"#fff","sort_order":0,"default":false}""")
            .RootElement;
        PlaneToWaypointMapper.MapState(json, Proj).Group.Should().Be(expected);
    }
}
