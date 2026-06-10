using FluentAssertions;
using Waypoint.Domain.Entities;
using Waypoint.Domain.Enums;
using Xunit;

namespace Waypoint.Domain.Tests.Entities;

/// <summary>
/// WAY-17: the Worklist entity's derived pointer/counters. Pure logic — exercised here at the
/// domain tier (the API integration tests cover the endpoints) so mutation testing has kills.
/// </summary>
public class WorklistEntityTests
{
    private static readonly Guid A = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid B = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly Guid C = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003");

    private static Worklist Active(int idx, params Guid[] ids) => new()
    {
        State = WorklistState.Active,
        OrderedIds = ids.ToList(),
        CurrentIdx = idx,
    };

    [Fact]
    public void CurrentId_is_the_item_at_the_pointer_when_active()
    {
        Active(0, A, B, C).CurrentId.Should().Be(A);
        Active(1, A, B, C).CurrentId.Should().Be(B);
        Active(2, A, B, C).CurrentId.Should().Be(C);
    }

    [Fact]
    public void CurrentId_is_null_when_inactive_even_with_a_valid_index()
    {
        var wl = Active(0, A, B, C);
        wl.State = WorklistState.Inactive;
        wl.CurrentId.Should().BeNull();
    }

    [Fact]
    public void CurrentId_is_null_when_pointer_is_at_or_past_the_end()
    {
        Active(3, A, B, C).CurrentId.Should().BeNull();   // idx == count (drained)
        Active(4, A, B, C).CurrentId.Should().BeNull();   // idx  > count
    }

    [Fact]
    public void CurrentId_is_null_when_the_queue_is_empty()
    {
        Active(0).CurrentId.Should().BeNull();
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 2)]
    [InlineData(2, 1)]
    [InlineData(3, 0)]    // exactly drained
    [InlineData(5, 0)]    // past the end clamps to 0 (kills "remove Math.Max")
    public void RemainingCount_is_items_left_clamped_at_zero(int idx, int expected)
    {
        Active(idx, A, B, C).RemainingCount.Should().Be(expected);
    }

    [Fact]
    public void SkippedCount_reflects_the_skipped_list()
    {
        var wl = Active(0, A, B, C);
        wl.SkippedCount.Should().Be(0);
        wl.Skipped.Add(new WorklistSkip { IssueId = A, Reason = "x" });
        wl.Skipped.Add(new WorklistSkip { IssueId = B, Reason = "y" });
        wl.SkippedCount.Should().Be(2);
    }

    [Fact]
    public void Defaults_are_inactive_empty_and_zeroed()
    {
        var wl = new Worklist();
        wl.State.Should().Be(WorklistState.Inactive);
        wl.OrderedIds.Should().BeEmpty();
        wl.Skipped.Should().BeEmpty();
        wl.CurrentIdx.Should().Be(0);
        wl.DoneCount.Should().Be(0);
        wl.CurrentId.Should().BeNull();
        wl.RemainingCount.Should().Be(0);
        new WorklistSkip().Reason.Should().BeEmpty();
    }
}
