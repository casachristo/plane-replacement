namespace Waypoint.Contracts;

public sealed record ActivityDto(
    Guid Id,
    string ActorType,
    Guid? ActorId,
    string? ActorLabel,
    string Verb,
    string? BeforeJson,
    string? AfterJson,
    DateTimeOffset At);
