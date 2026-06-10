using FluentAssertions;
using Waypoint.Importer.Mapping;
using Xunit;

namespace Waypoint.Domain.Tests.Importer;

/// <summary>
/// WAY-7: opt-in import path that lifts Markdown task-list checkboxes out of a Plane
/// ticket description into structured AcceptanceCriterion rows.
/// </summary>
public class AcceptanceCriteriaParserTests
{
    [Fact]
    public void Parses_unchecked_and_checked_boxes_in_order()
    {
        const string md = """
            Some intro text.

            - [ ] All tests pass
            - [x] Docs updated
            - [X] Migration applied
            """;

        var acs = PlaneToWaypointMapper.ParseAcceptanceCriteria(md);

        acs.Should().HaveCount(3);
        acs.Select(a => (a.Position, a.Text, a.Checked)).Should().Equal(
            (1, "All tests pass", false),
            (2, "Docs updated", true),
            (3, "Migration applied", true));
    }

    [Theory]
    [InlineData("- [ ] dash bullet")]
    [InlineData("* [ ] star bullet")]
    [InlineData("+ [ ] plus bullet")]
    [InlineData("   - [ ] indented bullet")]
    public void Accepts_dash_star_plus_and_indented_bullets(string line)
    {
        var acs = PlaneToWaypointMapper.ParseAcceptanceCriteria(line);
        acs.Should().ContainSingle();
        acs[0].Checked.Should().BeFalse();
    }

    [Fact]
    public void Ignores_non_checkbox_lines()
    {
        const string md = """
            # A heading
            - a plain bullet, not a checkbox
            Regular paragraph.
            1. an ordered item
            """;

        PlaneToWaypointMapper.ParseAcceptanceCriteria(md).Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Empty_or_null_description_yields_no_criteria(string? md)
    {
        PlaneToWaypointMapper.ParseAcceptanceCriteria(md).Should().BeEmpty();
    }

    [Fact]
    public void Trims_trailing_whitespace_from_text()
    {
        var acs = PlaneToWaypointMapper.ParseAcceptanceCriteria("- [ ] has trailing spaces   ");
        acs.Should().ContainSingle();
        acs[0].Text.Should().Be("has trailing spaces");
    }
}
