using FluentAssertions;
using Xunit;

namespace Waypoint.Domain.Tests.Importer;

/// <summary>
/// Light unit test for the importer label classifier. The full mapping pipeline gets
/// golden-file tests in tests/Waypoint.Importer.Tests once we have real Plane fixtures
/// to compare against.
/// </summary>
public class LabelClassifierTests
{
    [Theory]
    [InlineData("type:Bug", "issue_type", "Bug")]
    [InlineData("type: Feature", "issue_type", "Feature")]
    [InlineData("epic:Phase 1", "epic", "Phase 1")]
    [InlineData("EPIC:Phase 2", "epic", "Phase 2")]
    [InlineData("priority", "label", "priority")]
    [InlineData("frontend", "label", "frontend")]
    public void Classify_returns_expected_kind(string input, string expectedKind, string expectedName)
    {
        var (kind, name) = Waypoint.Importer.Mapping.PlaneToWaypointMapper.ClassifyLabel(input);
        kind.Should().Be(expectedKind);
        name.Should().Be(expectedName);
    }
}
