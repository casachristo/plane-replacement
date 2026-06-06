using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Waypoint.Api.Auth;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Domain;
using Waypoint.Domain.Entities;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

/// <summary>
/// Mutation-coverage for the /auth/post-login OIDC callback body and the /auth/logout
/// session-removal path. Uses <see cref="OidcTestAuthFactory"/> to present an
/// authenticated ctx.User built from request headers.
/// </summary>
public class AuthPostLoginMutationCoverage : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public AuthPostLoginMutationCoverage(PostgresFixture pg) => _pg = pg;

    private static OidcTestAuthFactory Factory(string conn) =>
        new() { PostgresConnectionString = conn };

    [Fact]
    public async Task Post_login_creates_user_session_and_cookie_for_new_oidc_user()
    {
        await using var f = Factory(_pg.ConnectionString);
        await f.EnsureMigratedAsync();
        var authority = f.Services.GetRequiredService<IOptions<OidcOptions>>().Value.Authority;
        using var client = f.CreateClient(new() { AllowAutoRedirect = false });
        var sub = "newuser-" + Guid.NewGuid().ToString("N");
        client.DefaultRequestHeaders.Add("X-Test-Sub", sub);
        client.DefaultRequestHeaders.Add("X-Test-Email", "alice@waypoint.test");
        client.DefaultRequestHeaders.Add("X-Test-Name", "Alice OIDC");
        client.DefaultRequestHeaders.Add("X-Test-Groups", "waypoint-admins, devs");

        var resp = await client.GetAsync("/auth/post-login");

        ((int)resp.StatusCode).Should().BeInRange(300, 399);
        resp.Headers.Location!.OriginalString.Should().Be("/");
        resp.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        var wpCookie = cookies!.Single(s => s.StartsWith(OidcSessionResolver.CookieName + "=", System.StringComparison.Ordinal));
        wpCookie.ToLowerInvariant().Should().Contain("httponly").And.Contain("secure").And.Contain("path=/");

        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WaypointDbContext>();
        var user = await db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.OidcSub == sub && u.OidcIssuer == authority);
        user.Should().NotBeNull();
        user!.Email.Should().Be("alice@waypoint.test");
        user.DisplayName.Should().Be("Alice OIDC");
        user.Groups.Should().Contain("waypoint-admins").And.Contain("devs");
        var hasSession = await db.UserSessions.AnyAsync(s => s.UserId == user.Id && s.ExpiresAt > DateTimeOffset.UtcNow);
        hasSession.Should().BeTrue();
    }

    [Fact]
    public async Task Post_login_updates_existing_user_and_sets_last_login()
    {
        await using var f = Factory(_pg.ConnectionString);
        await f.EnsureMigratedAsync();
        var authority = f.Services.GetRequiredService<IOptions<OidcOptions>>().Value.Authority;
        var sub = "existing-" + Guid.NewGuid().ToString("N");
        Guid userId;
        using (var seed = f.Services.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<WaypointDbContext>();
            var u = new User
            {
                Email = "old@waypoint.test",
                DisplayName = "Old Name",
                OidcSub = sub,
                OidcIssuer = authority,
                Groups = new[] { "stale" },
            };
            db.Users.Add(u);
            await db.SaveChangesAsync();
            userId = u.Id;
        }

        using var client = f.CreateClient(new() { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-Sub", sub);
        client.DefaultRequestHeaders.Add("X-Test-Email", "new@waypoint.test");
        client.DefaultRequestHeaders.Add("X-Test-Name", "New Name");
        client.DefaultRequestHeaders.Add("X-Test-Groups", "waypoint-admins");

        var resp = await client.GetAsync("/auth/post-login");
        ((int)resp.StatusCode).Should().BeInRange(300, 399);

        using var scope = f.Services.CreateScope();
        var db2 = scope.ServiceProvider.GetRequiredService<WaypointDbContext>();
        var user = await db2.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == userId);
        user.Email.Should().Be("new@waypoint.test");
        user.DisplayName.Should().Be("New Name");
        user.Groups.Should().BeEquivalentTo(new[] { "waypoint-admins" });
        user.LastLoginAt.Should().NotBeNull();
        user.LastLoginAt!.Value.Should().BeAfter(DateTimeOffset.UtcNow.AddMinutes(-2));
        (await db2.Users.IgnoreQueryFilters().CountAsync(u => u.OidcSub == sub)).Should().Be(1);
    }

    [Fact]
    public async Task Post_login_falls_back_to_email_when_name_claim_absent()
    {
        await using var f = Factory(_pg.ConnectionString);
        await f.EnsureMigratedAsync();
        using var client = f.CreateClient(new() { AllowAutoRedirect = false });
        var sub = "noname-" + Guid.NewGuid().ToString("N");
        client.DefaultRequestHeaders.Add("X-Test-Sub", sub);
        client.DefaultRequestHeaders.Add("X-Test-Email", "bob@waypoint.test");

        var resp = await client.GetAsync("/auth/post-login");
        ((int)resp.StatusCode).Should().BeInRange(300, 399);

        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WaypointDbContext>();
        var user = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.OidcSub == sub);
        user.DisplayName.Should().Be("bob@waypoint.test");
    }

    [Fact]
    public async Task Post_login_resolves_sub_from_raw_sub_claim_when_nameidentifier_absent()
    {
        await using var f = Factory(_pg.ConnectionString);
        await f.EnsureMigratedAsync();
        using var client = f.CreateClient(new() { AllowAutoRedirect = false });
        var sub = "rawsub-" + Guid.NewGuid().ToString("N");
        client.DefaultRequestHeaders.Add("X-Test-RawSub", sub);
        client.DefaultRequestHeaders.Add("X-Test-Email", "carol@waypoint.test");

        var resp = await client.GetAsync("/auth/post-login");
        ((int)resp.StatusCode).Should().BeInRange(300, 399);

        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WaypointDbContext>();
        (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.OidcSub == sub)).Should().BeTrue();
    }

    [Fact]
    public async Task Post_login_without_email_claim_fails_and_creates_no_user()
    {
        await using var f = Factory(_pg.ConnectionString);
        await f.EnsureMigratedAsync();
        using var client = f.CreateClient(new() { AllowAutoRedirect = false });
        var sub = "noemail-" + Guid.NewGuid().ToString("N");
        client.DefaultRequestHeaders.Add("X-Test-Sub", sub);

        var resp = await client.GetAsync("/auth/post-login");
        ((int)resp.StatusCode).Should().NotBe((int)HttpStatusCode.Redirect);
        ((int)resp.StatusCode).Should().BeGreaterThanOrEqualTo(400);

        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WaypointDbContext>();
        (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.OidcSub == sub)).Should().BeFalse();
    }

    [Fact]
    public async Task Post_login_reads_email_from_standard_claim_when_primary_email_claim_absent()
    {
        await using var f = Factory(_pg.ConnectionString);
        await f.EnsureMigratedAsync();
        using var client = f.CreateClient(new() { AllowAutoRedirect = false });
        var sub = "emailfallback-" + Guid.NewGuid().ToString("N");
        client.DefaultRequestHeaders.Add("X-Test-Sub", sub);
        client.DefaultRequestHeaders.Add("X-Test-EmailFallback", "dave@waypoint.test");

        var resp = await client.GetAsync("/auth/post-login");
        ((int)resp.StatusCode).Should().BeInRange(300, 399);

        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WaypointDbContext>();
        var user = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.OidcSub == sub);
        user.Email.Should().Be("dave@waypoint.test");
    }

    [Fact]
    public async Task Logout_removes_matching_session_row()
    {
        await using var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        const string cookieValue = "real-cookie-value-xyz";
        Guid sessionId;
        using (var seed = f.Services.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<WaypointDbContext>();
            var u = new User { Email = "logout@waypoint.test", DisplayName = "Logout User" };
            db.Users.Add(u);
            await db.SaveChangesAsync();
            var session = new UserSession
            {
                UserId = u.Id,
                CookieHash = OidcSessionResolver.HashCookie(cookieValue),
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
            };
            db.UserSessions.Add(session);
            await db.SaveChangesAsync();
            sessionId = session.Id;
        }

        using var client = f.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", OidcSessionResolver.CookieName + "=" + cookieValue);
        var resp = await client.PostAsync("/auth/logout", content: null);
        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = f.Services.CreateScope();
        var db2 = scope.ServiceProvider.GetRequiredService<WaypointDbContext>();
        (await db2.UserSessions.AnyAsync(s => s.Id == sessionId)).Should().BeFalse();
    }
}
