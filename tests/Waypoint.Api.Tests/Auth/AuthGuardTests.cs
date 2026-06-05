using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Waypoint.Api.Auth;
using Waypoint.Domain;
using Xunit;

namespace Waypoint.Api.Tests.Auth;

/// <summary>
/// Mutation-coverage tests for AuthGuard.RequireAuth + RequireScope. Drives the
/// helpers directly with a DefaultHttpContext so we exercise every branch
/// (principal-present, principal-absent, scope-present, scope-absent, scope-with-
/// case-sensitivity).
/// </summary>
public class AuthGuardTests
{
    private static DefaultHttpContext WithPrincipal(Principal? p)
    {
        var ctx = new DefaultHttpContext();
        if (p is not null) ctx.Items[PrincipalMiddleware.ItemKey] = p;
        return ctx;
    }

    private static Principal Human(params string[] scopes) =>
        new(PrincipalKind.Human, "user-1", "Alice", scopes);

    [Fact]
    public void RequireAuth_returns_principal_when_present()
    {
        var p = Human();
        var ctx = WithPrincipal(p);

        var got = AuthGuard.RequireAuth(ctx);

        got.Should().BeSameAs(p);
    }

    [Fact]
    public void RequireAuth_throws_UnauthorizedException_when_principal_absent()
    {
        var ctx = WithPrincipal(null);

        Action act = () => AuthGuard.RequireAuth(ctx);

        act.Should().Throw<UnauthorizedException>()
            .Which.Code.Should().Be("unauthenticated");
    }

    [Fact]
    public void RequireAuth_thrown_exception_has_401_status()
    {
        var ctx = WithPrincipal(null);

        Action act = () => AuthGuard.RequireAuth(ctx);

        act.Should().Throw<UnauthorizedException>()
            .Which.StatusCode.Should().Be(401);
    }

    [Fact]
    public void RequireScope_returns_principal_when_scope_present()
    {
        var p = Human("admin", "read");
        var ctx = WithPrincipal(p);

        var got = AuthGuard.RequireScope(ctx, "admin");

        got.Should().BeSameAs(p);
    }

    [Fact]
    public void RequireScope_throws_ValidationException_when_scope_absent()
    {
        var p = Human("read", "write");
        var ctx = WithPrincipal(p);

        Action act = () => AuthGuard.RequireScope(ctx, "admin");

        act.Should().Throw<ValidationException>()
            .Which.Code.Should().Be("missing_scope");
    }

    [Fact]
    public void RequireScope_validation_exception_carries_required_scope_in_details()
    {
        var p = Human("read");
        var ctx = WithPrincipal(p);

        Action act = () => AuthGuard.RequireScope(ctx, "admin");

        act.Should().Throw<ValidationException>()
            .Which.Details!["required"].Should().Be("admin");
    }

    [Fact]
    public void RequireScope_is_case_sensitive()
    {
        // Kills: StringComparer.Ordinal → StringComparer.OrdinalIgnoreCase mutation.
        var p = Human("Admin");   // capital A
        var ctx = WithPrincipal(p);

        Action act = () => AuthGuard.RequireScope(ctx, "admin");

        act.Should().Throw<ValidationException>()
            .Which.Code.Should().Be("missing_scope");
    }

    [Fact]
    public void RequireScope_throws_Unauthorized_when_no_principal_at_all()
    {
        // RequireScope calls RequireAuth first — if no principal, that path fires
        // BEFORE we ever check Scopes. Pins which exception comes out.
        var ctx = WithPrincipal(null);

        Action act = () => AuthGuard.RequireScope(ctx, "admin");

        act.Should().Throw<UnauthorizedException>()
            .Which.Code.Should().Be("unauthenticated");
    }
}
