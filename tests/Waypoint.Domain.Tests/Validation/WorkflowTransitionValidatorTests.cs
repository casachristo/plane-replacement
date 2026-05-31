using FluentAssertions;
using Waypoint.Domain.Validation;
using Xunit;

namespace Waypoint.Domain.Tests.Validation;

public class WorkflowTransitionValidatorTests
{
    private static readonly Guid Backlog = Guid.NewGuid();
    private static readonly Guid InProgress = Guid.NewGuid();
    private static readonly Guid Done = Guid.NewGuid();

    private static readonly (Guid From, Guid To)[] Allowed =
    [
        (Backlog, InProgress),
        (InProgress, Done),
        (Done, InProgress),
    ];

    [Fact]
    public void Allowed_transition_returns_valid()
    {
        var sut = new WorkflowTransitionValidator(Allowed);
        sut.CanTransition(Backlog, InProgress).Should().BeTrue();
    }

    [Fact]
    public void Disallowed_transition_returns_invalid()
    {
        var sut = new WorkflowTransitionValidator(Allowed);
        sut.CanTransition(Done, Backlog).Should().BeFalse();
    }

    [Fact]
    public void Same_state_is_always_allowed_noop()
    {
        var sut = new WorkflowTransitionValidator(Allowed);
        sut.CanTransition(Backlog, Backlog).Should().BeTrue();
    }

    [Fact]
    public void Empty_workflow_rejects_everything_except_noops()
    {
        var sut = new WorkflowTransitionValidator([]);
        sut.CanTransition(Backlog, InProgress).Should().BeFalse();
        sut.CanTransition(Backlog, Backlog).Should().BeTrue();
    }
}
