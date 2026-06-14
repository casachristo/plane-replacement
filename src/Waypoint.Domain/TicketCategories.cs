using Waypoint.Domain.Enums;

namespace Waypoint.Domain;

/// <summary>
/// WAY-24: parse/format helpers for <see cref="TicketCategory"/> on the wire (lowercase name)
/// and a mapping from the Plane Community type-labels Waypoint is replacing.
/// </summary>
public static class TicketCategories
{
    /// <summary>Wire form: the lowercase enum name (e.g. "bug", "feature", "brainstorm").</summary>
    public static string ToWire(TicketCategory c) => c.ToString().ToLowerInvariant();

    /// <summary>
    /// Parse a wire string (case-insensitive). Returns false for unknown values so the API can
    /// reject them with a 400 rather than silently coercing to the default.
    /// </summary>
    public static bool TryParse(string? value, out TicketCategory category)
    {
        category = TicketCategory.Feature;
        if (string.IsNullOrWhiteSpace(value)) return false;
        return Enum.TryParse(value.Trim(), ignoreCase: true, out category)
               && Enum.IsDefined(category);
    }

    /// <summary>
    /// Map a Plane Community type-label to a Waypoint category. Unknown / absent labels map to
    /// <see cref="TicketCategory.Feature"/>. "coding" is an alias for feature.
    /// </summary>
    public static TicketCategory FromPlaneLabel(string? label) =>
        (label ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "brainstorm" or "spike" => TicketCategory.Brainstorm,
            "bug" => TicketCategory.Bug,
            "infra" => TicketCategory.Infra,
            "security" => TicketCategory.Security,
            "chore" => TicketCategory.Chore,
            "test" => TicketCategory.Test,
            "docs" => TicketCategory.Docs,
            "feature" or "coding" or "" => TicketCategory.Feature,
            _ => TicketCategory.Feature,
        };
}
