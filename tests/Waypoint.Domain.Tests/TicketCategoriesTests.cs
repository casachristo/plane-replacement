using FluentAssertions;
using Waypoint.Domain;
using Waypoint.Domain.Enums;
using Xunit;

namespace Waypoint.Domain.Tests;

// WAY-24: pure mapping/parse for the first-class ticket taxonomy.
public class TicketCategoriesTests
{
    [Theory]
    [InlineData(TicketCategory.Feature, "feature")]
    [InlineData(TicketCategory.Brainstorm, "brainstorm")]
    [InlineData(TicketCategory.Bug, "bug")]
    [InlineData(TicketCategory.Docs, "docs")]
    public void ToWire_is_the_lowercase_name(TicketCategory c, string expected) =>
        TicketCategories.ToWire(c).Should().Be(expected);

    [Theory]
    [InlineData("bug", TicketCategory.Bug)]
    [InlineData("BUG", TicketCategory.Bug)]
    [InlineData("  Security ", TicketCategory.Security)]
    public void TryParse_accepts_known_values_case_insensitively(string input, TicketCategory expected)
    {
        TicketCategories.TryParse(input, out var c).Should().BeTrue();
        c.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("nonsense")]
    [InlineData("123")]
    public void TryParse_rejects_unknown_or_empty(string? input) =>
        TicketCategories.TryParse(input, out _).Should().BeFalse();

    [Theory]
    [InlineData("coding", TicketCategory.Feature)]   // alias
    [InlineData("feature", TicketCategory.Feature)]
    [InlineData("spike", TicketCategory.Brainstorm)]
    [InlineData("infra", TicketCategory.Infra)]
    [InlineData("unmapped-label", TicketCategory.Feature)]  // safe default
    [InlineData(null, TicketCategory.Feature)]
    public void FromPlaneLabel_maps_legacy_labels(string? label, TicketCategory expected) =>
        TicketCategories.FromPlaneLabel(label).Should().Be(expected);
}
