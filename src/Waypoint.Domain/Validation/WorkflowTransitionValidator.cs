namespace Waypoint.Domain.Validation;

public sealed class WorkflowTransitionValidator
{
    private readonly HashSet<(Guid From, Guid To)> _allowed;

    public WorkflowTransitionValidator(IEnumerable<(Guid From, Guid To)> allowedTransitions)
    {
        _allowed = [.. allowedTransitions];
    }

    public bool CanTransition(Guid fromStateId, Guid toStateId)
    {
        if (fromStateId == toStateId) return true;
        return _allowed.Contains((fromStateId, toStateId));
    }
}
