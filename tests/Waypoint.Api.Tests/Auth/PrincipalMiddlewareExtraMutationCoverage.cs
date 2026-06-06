using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Waypoint.Api.Auth;
using Waypoint.Domain;
using Xunit;

namespace Waypoint.Api.Tests.Auth;

public class PrincipalMiddlewareExtraMutationCoverage
{
    private sealed class FixedResolver(Principal? p) : IPrincipalResolver
    {
        public Task<Principal?> ResolveAsync(HttpContext ctx, CancellationToken ct) => Task.FromResult(p);
    }

    private sealed class ThrowingResolver : IPrincipalResolver
    {
        public Task<Principal?> ResolveAsync(HttpContext ctx, CancellationToken ct)
            => throw new InvalidOperationException("should not be invoked after a successful resolver");
    }

    [Fact]
    public async Task First_successful_resolver_short_circuits_later_resolvers()
    {
        var p = new Principal(PrincipalKind.Human, "u-1", "U", []);
        var mw = new PrincipalMiddleware(_ => Task.CompletedTask);
        var ctx = new DefaultHttpContext();

        // If the throwing resolver is reached, ResolveAsync would throw.
        // It must NOT be reached because the first resolver already returned non-null.
        await mw.InvokeAsync(ctx, [new FixedResolver(p), new ThrowingResolver()]);

        ctx.GetPrincipal().Should().BeSameAs(p);
    }

    [Fact]
    public async Task Empty_resolver_list_leaves_GetPrincipal_null_and_still_calls_next()
    {
        var nextCalled = false;
        var mw = new PrincipalMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var ctx = new DefaultHttpContext();
        await mw.InvokeAsync(ctx, Array.Empty<IPrincipalResolver>());
        nextCalled.Should().BeTrue();
        ctx.GetPrincipal().Should().BeNull();
    }

    [Fact]
    public async Task GetPrincipal_extension_returns_null_when_Items_key_value_is_not_a_Principal()
    {
        var ctx = new DefaultHttpContext();
        ctx.Items[PrincipalMiddleware.ItemKey] = "not-a-principal";
        ctx.GetPrincipal().Should().BeNull();
    }
}
