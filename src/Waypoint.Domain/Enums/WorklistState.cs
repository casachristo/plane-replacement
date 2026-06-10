namespace Waypoint.Domain.Enums;

/// <summary>
/// WAY-17: a project's singleton batch Worklist is either dormant or actively draining.
/// Stored as text ('inactive' | 'active') so the wire/DB value is self-describing.
/// </summary>
public enum WorklistState
{
    Inactive,
    Active,
}
