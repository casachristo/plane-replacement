using FluentAssertions;
using Waypoint.Api.Auth;
using Xunit;

namespace Waypoint.Api.Tests.Auth;

/// <summary>
/// Mutation-coverage tests for OidcOptions defaults. The class is bound from
/// config; its defaults define the &quot;works on a fresh homelab&quot; baseline.
/// These tests pin those defaults so silent rewriting of the constants is
/// caught (Stryker mutates string and array literals here).
/// </summary>
public class OidcOptionsTests
{
    [Fact]
    public void Authority_defaults_to_homelab_auth_url()
    {
        new OidcOptions().Authority.Should().Be("https://auth.chris.box");
    }

    [Fact]
    public void ClientId_defaults_to_waypoint()
    {
        new OidcOptions().ClientId.Should().Be("waypoint");
    }

    [Fact]
    public void ClientSecret_defaults_to_empty()
    {
        new OidcOptions().ClientSecret.Should().BeEmpty();
    }

    [Fact]
    public void RedirectUri_defaults_to_auth_callback_path()
    {
        new OidcOptions().RedirectUri.Should().EndWith("/auth/callback");
    }

    [Fact]
    public void Scopes_default_includes_openid_profile_email_groups()
    {
        new OidcOptions().Scopes.Should().BeEquivalentTo(new[] { "openid", "profile", "email", "groups" });
    }

    [Fact]
    public void Claim_name_defaults_match_standard_OIDC()
    {
        var o = new OidcOptions();
        o.EmailClaim.Should().Be("email");
        o.NameClaim.Should().Be("name");
        o.GroupsClaim.Should().Be("groups");
    }

    [Fact]
    public void AdminGroups_default_includes_waypoint_admins()
    {
        new OidcOptions().AdminGroups.Should().Contain("waypoint-admins");
    }

    [Fact]
    public void SessionLifetime_defaults_to_30_days()
    {
        new OidcOptions().SessionLifetime.Should().Be(TimeSpan.FromDays(30));
    }

    [Fact]
    public void SectionName_is_Auth_Oidc()
    {
        OidcOptions.SectionName.Should().Be("Auth:Oidc");
    }
}
