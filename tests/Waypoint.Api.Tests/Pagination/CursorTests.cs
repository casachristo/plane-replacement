using FluentAssertions;
using Waypoint.Api.Pagination;
using Xunit;

namespace Waypoint.Api.Tests.Pagination;

public class CursorTests
{
    [Fact]
    public void Encode_then_Decode_round_trips_a_DateTimeOffset_and_Guid()
    {
        var ts = new DateTimeOffset(2026, 5, 30, 12, 0, 0, TimeSpan.Zero);
        var id = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var encoded = Cursor.Encode(ts, id);
        var (decTs, decId) = Cursor.Decode(encoded);
        decTs.Should().Be(ts);
        decId.Should().Be(id);
    }

    [Fact]
    public void Decode_throws_on_malformed_input()
    {
        Action act = () => Cursor.Decode("not-a-cursor");
        act.Should().Throw<FormatException>();
    }
}
