namespace Waypoint.Domain.Enums;

/// <summary>
/// WAY-24: Waypoint's first-class ticket taxonomy, replacing Plane Community type-labels.
/// Stored as int. <see cref="Feature"/> is the default (value 0) so pre-existing rows and
/// callers that omit a category land on the most common case. <see cref="Brainstorm"/> is an
/// idea/spike whose output is a decision, not code — the UI uses it to pick a decision-doc
/// acceptance template instead of a tests template.
/// </summary>
public enum TicketCategory
{
    Feature = 0,
    Brainstorm = 1,
    Bug = 2,
    Infra = 3,
    Security = 4,
    Chore = 5,
    Test = 6,
    Docs = 7,
}
