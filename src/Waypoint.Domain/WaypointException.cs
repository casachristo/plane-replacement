namespace Waypoint.Domain;

public class WaypointException : Exception
{
    public string Code { get; }
    public int StatusCode { get; }
    public IReadOnlyDictionary<string, object>? Details { get; }

    public WaypointException(string code, string message, int statusCode, IReadOnlyDictionary<string, object>? details = null)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
        Details = details;
    }
}

public sealed class NotFoundException(string code, string message) : WaypointException(code, message, 404);

public sealed class ConflictException(string code, string message, IReadOnlyDictionary<string, object>? details = null)
    : WaypointException(code, message, 409, details);

public sealed class ValidationException(string code, string message, IReadOnlyDictionary<string, object>? details = null)
    : WaypointException(code, message, 422, details);
