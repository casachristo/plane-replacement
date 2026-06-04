namespace Waypoint.Contracts;

public sealed record CreateAcceptanceCriterionRequest(string Text, int? Position = null);
