namespace Waypoint.Contracts;

public sealed record LabelDto(
    Guid Id,
    string Name,
    string Color,
    Guid? ParentLabelId);
