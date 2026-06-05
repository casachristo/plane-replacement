using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Waypoint.Api.Auth;
using Waypoint.Domain;
using Xunit;

namespace Waypoint.Api.Tests.Auth;

/// <summary>
/// Mutation-coverage tests for PrincipalMiddleware. The middleware runs every
/// registered IPrincipalResolver in order and stashes the first non-null result;
/// downstream code reads via HttpContext.GetPrincipal().
/// </summary>
public class PrincipalMiddlewareTests
{
    private sealed class FixedResolver(Principal? p) : IPrincipalResolver
    {
        public Task<Principal?> ResolveAsync(HttpContext ctx, CancellationToken ct) => Task.FromResult(p);
    }

    private static Principal Human() => new(PrincipalKind.Human, "u-1", "Alice", []);
    private static Principal Service() => new(PrincipalKind.InternalService, "00000000-0000-0000-0000-000000000001", "agent", []);

    [Fact]
    public async Task First_non_null_resolver_wins()
    {
        var p = Human();
        var middleware = new PrincipalMiddleware(_ => Task.CompletedTask);
        var ctx = new DefaultHttpContext();
        await middleware.InvokeAsync(ctx, [new FixedResolver(p), new FixedResolver(Service())]);
        ctx.GetPrincipal().Should().BeSameAs(p);
    }

    [Fact]
    public async Task Skips_null_resolvers_until_one_returns_a_principal()
    {
        var p = Service();
        var middleware = new PrincipalMiddleware(_ => Task.CompletedTask);
        var ctx = new DefaultHttpContext();
        await middleware.InvokeAsync(ctx, [new FixedResolver(null), new FixedResolver(p)]);
        ctx.GetPrincipal().Should().BeSameAs(p);
    }

    [Fact]
    public async Task All_resolvers_null_leaves_GetPrincipal_null()
    {
        var middleware = new PrincipalMiddleware(_ => Task.CompletedTask);
        var ctx = new DefaultHttpContext();
        await middleware.InvokeAsync(ctx, [new FixedResolver(null), new FixedResolver(null)]);
        ctx.GetPrincipal().Should().BeNull();
    }

    [Fact]
    public async Task Always_calls_next_even_when_no_resolver_matches()
    {
        var nextCalled = false;
        var middleware = new PrincipalMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var ctx = new DefaultHttpContext();
        await middleware.InvokeAsync(ctx, [new FixedResolver(null)]);
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public void ItemKey_constant_is_Principal()
    {
        PrincipalMiddleware.ItemKey.Should().Be("Principal");
    }
}
