using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Waypoint.Api.Auth;
using Waypoint.Api.Subsystems.Identity.Sessions;

namespace Waypoint.Api.Endpoints.PublicApi;

/// <summary>
/// OIDC + waypoint_session cookie flow. The /auth/login redirects through the OIDC
/// challenge, /auth/callback is wired up by the framework, but on successful auth we
/// upsert the user, issue our own session cookie, and clear the OIDC scratch cookies.
/// User/session persistence lives in the Sessions subsystem (ISessionService); this endpoint
/// only extracts the OIDC claims and writes the response cookie.
/// </summary>
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/auth/login", (HttpContext ctx) =>
        {
            // If the caller already has a valid waypoint_session, short-circuit straight to
            // the home page. Without this, hitting /auth/login while logged in triggers a fresh
            // OIDC challenge and inserts a duplicate UserSession row — every refresh of the
            // login link leaked a session over time.
            if (ctx.GetPrincipal() is { Kind: PrincipalKind.Human }) return Results.Redirect("/");
            return Results.Challenge(
                new AuthenticationProperties { RedirectUri = "/auth/post-login" },
                [OpenIdConnectDefaults.AuthenticationScheme]);
        });

        app.MapGet("/auth/post-login", async (HttpContext ctx, ISessionService sessions,
            IOptions<OidcOptions> oidcOpts, CancellationToken ct) =>
        {
            if (ctx.User.Identity?.IsAuthenticated != true) return Results.Unauthorized();

            var opts = oidcOpts.Value;
            var sub = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? ctx.User.FindFirst("sub")?.Value
                ?? throw new InvalidOperationException("OIDC token missing sub claim");
            var email = ctx.User.FindFirst(opts.EmailClaim)?.Value
                ?? ctx.User.FindFirst(ClaimTypes.Email)?.Value
                ?? throw new InvalidOperationException("OIDC token missing email claim");
            var name = ctx.User.FindFirst(opts.NameClaim)?.Value
                ?? ctx.User.FindFirst(ClaimTypes.Name)?.Value
                ?? email;
            var groups = ctx.User.FindAll(opts.GroupsClaim).Select(c => c.Value).ToArray();

            var (cookieValue, expiresAt) = await sessions.EstablishSessionAsync(
                opts.Authority, sub, email, name, groups,
                ctx.Connection.RemoteIpAddress?.ToString(), ctx.Request.Headers.UserAgent.ToString(), ct);

            // Sign out the ASP.NET Core OIDC cookie FIRST so my waypoint_session Append isn't
            // wiped by the auth pipeline's response writer. Then set the long-lived session cookie.
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            ctx.Response.Cookies.Append(OidcSessionResolver.CookieName, cookieValue, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,                   // Traefik fronts HTTPS; cookie must never travel plaintext
                SameSite = SameSiteMode.Lax,
                Path = "/",
                Expires = expiresAt,
            });

            return Results.Redirect("/");
        });

        app.MapPost("/auth/logout", async (HttpContext ctx, ISessionService sessions, CancellationToken ct) =>
        {
            if (ctx.Request.Cookies.TryGetValue(OidcSessionResolver.CookieName, out var cookie) && !string.IsNullOrEmpty(cookie))
                await sessions.LogoutAsync(cookie, ct);
            ctx.Response.Cookies.Delete(OidcSessionResolver.CookieName);
            return Results.NoContent();
        });

        app.MapGet("/api/v1/whoami", (HttpContext ctx) =>
        {
            // AuthGuard.RequireAuth throws UnauthorizedException → 401 with the standard JSON
            // envelope via ErrorEnvelopeMiddleware. Results.Unauthorized() would skip the
            // envelope and break shared 401-handling clients.
            var principal = Waypoint.Api.Auth.AuthGuard.RequireAuth(ctx);
            return Results.Ok(new
            {
                kind = principal.Kind.ToString(),
                id = principal.Id,
                displayName = principal.DisplayName,
                scopes = principal.Scopes,
            });
        });
    }
}
