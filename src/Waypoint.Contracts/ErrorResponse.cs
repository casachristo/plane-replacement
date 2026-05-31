namespace Waypoint.Contracts;

public sealed record ErrorResponse(ErrorBody Error, string RequestId);

public sealed record ErrorBody(string Code, string Message, IReadOnlyDictionary<string, object>? Details = null);
