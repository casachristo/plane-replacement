using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Waypoint.Api.Tests.Fixtures;

/// <summary>
/// Variant of <see cref="WaypointApiFactory"/> that wires a fake OIDC authenticate
/// scheme so tests can drive the /auth/post-login callback, which reads the ASP.NET
/// Core <c>ctx.User</c> ClaimsPrincipal (NOT the waypoint Principal). The handler only
/// authenticates when an <c>X-Test-Sub</c>/<c>X-Test-RawSub</c> header is present, so
/// requests without those headers still see an anonymous ctx.User exactly as before.
/// </summary>
public sealed class OidcTestAuthFactory : WaypointApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, TestOidcAuthHandler>("TestOidc", _ => { });
            // UseAuthentication populates ctx.User from the default *authenticate* scheme.
            // Point it at TestOidc; the challenge scheme (OIDC) is left untouched so
            // /auth/login still behaves normally.
            services.Configure<AuthenticationOptions>(o => o.DefaultAuthenticateScheme = "TestOidc");
        });
    }
}

public sealed class TestOidcAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestOidcAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder) : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var sub = Request.Headers["X-Test-Sub"].ToString();
        var rawSub = Request.Headers["X-Test-RawSub"].ToString();
        if (string.IsNullOrEmpty(sub) && string.IsNullOrEmpty(rawSub))
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new List<Claim>();
        if (!string.IsNullOrEmpty(sub)) claims.Add(new Claim(ClaimTypes.NameIdentifier, sub));
        if (!string.IsNullOrEmpty(rawSub)) claims.Add(new Claim("sub", rawSub));

        var email = Request.Headers["X-Test-Email"].ToString();
        if (!string.IsNullOrEmpty(email)) claims.Add(new Claim("email", email));
        var emailFallback = Request.Headers["X-Test-EmailFallback"].ToString();
        if (!string.IsNullOrEmpty(emailFallback)) claims.Add(new Claim(ClaimTypes.Email, emailFallback));

        var name = Request.Headers["X-Test-Name"].ToString();
        if (!string.IsNullOrEmpty(name)) claims.Add(new Claim("name", name));
        var nameFallback = Request.Headers["X-Test-NameFallback"].ToString();
        if (!string.IsNullOrEmpty(nameFallback)) claims.Add(new Claim(ClaimTypes.Name, nameFallback));

        var groups = Request.Headers["X-Test-Groups"].ToString();
        if (!string.IsNullOrEmpty(groups))
            foreach (var g in groups.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                claims.Add(new Claim("groups", g));

        var identity = new ClaimsIdentity(claims, "TestOidc");
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), "TestOidc");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
