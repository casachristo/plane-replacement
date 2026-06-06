using FluentAssertions;
using Waypoint.Domain.Entities;
using Xunit;

namespace Waypoint.Domain.Tests.Entities;

/// <summary>
/// Pins the default values entities land on when only their required members are set.
/// These defaults are load-bearing — the API serializes a fresh entity straight back to
/// the client (e.g. a new issue is "To Do" with an empty body), so a silent change to a
/// default literal is a real regression. Each assertion also kills the corresponding
/// Stryker default-value mutant in the Domain mutation gate.
/// </summary>
public class EntityDefaultsTests
{
    [Fact]
    public void Cycle_defaults_State_to_upcoming()
    {
        var c = new Cycle { Name = "Sprint 1" };
        c.State.Should().Be("upcoming");
    }

    [Fact]
    public void Epic_defaults_DescriptionMd_empty_and_Status_planned()
    {
        var e = new Epic { Title = "Auth" };
        e.DescriptionMd.Should().Be(string.Empty);
        e.Status.Should().Be("planned");
    }

    [Fact]
    public void Issue_defaults_DescriptionMd_to_empty()
    {
        var i = new Issue { Title = "Login bug" };
        i.DescriptionMd.Should().Be(string.Empty);
    }

    [Fact]
    public void WebhookSubscription_defaults_to_active()
    {
        var s = new WebhookSubscription { TargetUrl = "https://example.test/hook", Secret = "s3cr3t" };
        s.IsActive.Should().BeTrue();
    }

    [Fact]
    public void WebhookDelivery_defaults_Status_to_pending()
    {
        var d = new WebhookDelivery { Event = "issue.created", PayloadJson = "{}" };
        d.Status.Should().Be("pending");
    }
}
