using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Waypoint.Api.Auth;
using Waypoint.Domain;
using Waypoint.Domain.Entities;

namespace Waypoint.Api.Endpoints.PublicApi;

/// <summary>
/// OIDC + waypoint_session cookie flow. The /auth/login redirects through the OIDC
/// challenge, /auth/callback is wired up by the framework, but on successful auth we
/// upsert the user, issue our own session cookie, and clear the OIDC scratch cookies.
/// </summary>
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/auth/login", (HttpContext ctx) =>
            Results.Challenge(
                new AuthenticationProperties { RedirectUri = "/auth/post-login" },
                [OpenIdConnectDefaults.AuthenticationScheme]));

        app.MapGet("/auth/post-login", async (HttpContext ctx, WaypointDbContext db,
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

            var user = await db.Users.FirstOrDefaultAsync(u => u.OidcIssuer == opts.Authority && u.OidcSub == sub, ct);
            if (user is null)
            {
                user = new User { Email = email, DisplayName = name, OidcSub = sub, OidcIssuer = opts.Authority, Groups = groups };
                db.Users.Add(user);
            }
            else
            {
                user.Email = email;
                user.DisplayName = name;
                user.Groups = groups;
                user.LastLoginAt = DateTimeOffset.UtcNow;
            }
            await db.SaveChangesAsync(ct);

            var cookieValue = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            var session = new UserSession
            {
                UserId = user.Id,
                CookieHash = OidcSessionResolver.HashCookie(cookieValue),
                ExpiresAt = DateTimeOffset.UtcNow + opts.SessionLifetime,
                Ip = ctx.Connection.RemoteIpAddress?.ToString(),
                UserAgent = ctx.Request.Headers.UserAgent.ToString(),
            };
            db.UserSessions.Add(session);
            await db.SaveChangesAsync(ct);

            // Sign out the ASP.NET Core OIDC cookie FIRST so my waypoint_session Append isn't
            // wiped by the auth pipeline's response writer. Then set the long-lived session cookie.
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            ctx.Response.Cookies.Append(OidcSessionResolver.CookieName, cookieValue, new CookieOptions
            {
                HttpOnly = true,
                Secure = ctx.Request.IsHttps,    // honor whatever the framework thinks of the request
                SameSite = SameSiteMode.Lax,
                Path = "/",
                Expires = session.ExpiresAt,
            });

            return Results.Redirect("/");
        });

        app.MapPost("/auth/logout", async (HttpContext ctx, WaypointDbContext db, CancellationToken ct) =>
        {
            if (ctx.Request.Cookies.TryGetValue(OidcSessionResolver.CookieName, out var cookie) && !string.IsNullOrEmpty(cookie))
            {
                var hash = OidcSessionResolver.HashCookie(cookie);
                var session = await db.UserSessions.FirstOrDefaultAsync(s => s.CookieHash == hash, ct);
                if (session is not null) db.UserSessions.Remove(session);
                await db.SaveChangesAsync(ct);
            }
            ctx.Response.Cookies.Delete(OidcSessionResolver.CookieName);
            return Results.NoContent();
        });

        app.MapGet("/auth/debug-cookies", (HttpContext ctx) =>
        {
            return Results.Ok(new
            {
                cookiesReceived = ctx.Request.Cookies.Keys.ToArray(),
                isHttps = ctx.Request.IsHttps,
                scheme = ctx.Request.Scheme,
                host = ctx.Request.Host.Value,
                xForwardedProto = ctx.Request.Headers["X-Forwarded-Proto"].ToString(),
                xForwardedHost = ctx.Request.Headers["X-Forwarded-Host"].ToString(),
                remoteIp = ctx.Connection.RemoteIpAddress?.ToString(),
            });
        });

        app.MapGet("/auth/debug-setcookie", (HttpContext ctx) =>
        {
            ctx.Response.Cookies.Append("debug_test", "hello", new CookieOptions
            {
                HttpOnly = false, Secure = ctx.Request.IsHttps, SameSite = SameSiteMode.Lax, Path = "/",
                Expires = DateTimeOffset.UtcNow.AddHours(1),
            });
            return Results.Ok(new { setCookie = "debug_test=hello", isHttps = ctx.Request.IsHttps });
        });

        app.MapGet("/api/v1/whoami", (HttpContext ctx) =>
        {
            var principal = ctx.GetPrincipal();
            if (principal is null) return Results.Unauthorized();
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
