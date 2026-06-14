using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Waypoint.Api.Auth;
using Waypoint.Api.Subsystems.Identity.Tokens;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Domain;
using Xunit;

namespace Waypoint.Api.Tests.Auth;

/// <summary>
/// Mutation-coverage tests for AuditLogMiddleware. The middleware appends a
/// TokenAuditLog row for every request whose principal is InternalService and
/// whose Id parses as a Guid; otherwise it silently skips.
/// </summary>
public class AuditLogMiddlewareMutationCoverage : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public AuditLogMiddlewareMutationCoverage(PostgresFixture pg) => _pg = pg;

    private async Task<(WaypointApiFactory factory, WaypointDbContext db)> NewFactoryAndDb()
    {
        var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        var scope = factory.Services.CreateScope();
        return (factory, scope.ServiceProvider.GetRequiredService<WaypointDbContext>());
    }

    private static DefaultHttpContext WithPrincipal(Principal? p, string method = "GET", string path = "/test")
    {
        var ctx = new DefaultHttpContext();
        if (p is not null) ctx.Items[PrincipalMiddleware.ItemKey] = p;
        ctx.Request.Method = method;
        ctx.Request.Path = path;
        ctx.Response.StatusCode = 200;
        return ctx;
    }

    [Fact]
    public async Task No_principal_writes_no_audit_row()
    {
        var (factory, db) = await NewFactoryAndDb();
        await using (factory)
        {
            var before = await db.TokenAuditLog.CountAsync();
            var mw = new AuditLogMiddleware(_ => Task.CompletedTask);
            await mw.InvokeAsync(WithPrincipal(null), new TokenService(new TokenManager(db)));
            var after = await db.TokenAuditLog.CountAsync();
            after.Should().Be(before);
        }
    }

    [Fact]
    public async Task Human_principal_writes_no_audit_row()
    {
        var (factory, db) = await NewFactoryAndDb();
        await using (factory)
        {
            var before = await db.TokenAuditLog.CountAsync();
            var mw = new AuditLogMiddleware(_ => Task.CompletedTask);
            var human = new Principal(PrincipalKind.Human, Guid.NewGuid().ToString(), "u", []);
            await mw.InvokeAsync(WithPrincipal(human), new TokenService(new TokenManager(db)));
            var after = await db.TokenAuditLog.CountAsync();
            after.Should().Be(before);
        }
    }

    [Fact]
    public async Task InternalService_with_non_Guid_id_writes_no_audit_row()
    {
        var (factory, db) = await NewFactoryAndDb();
        await using (factory)
        {
            var before = await db.TokenAuditLog.CountAsync();
            var mw = new AuditLogMiddleware(_ => Task.CompletedTask);
            var bad = new Principal(PrincipalKind.InternalService, "not-a-guid", "svc", []);
            await mw.InvokeAsync(WithPrincipal(bad), new TokenService(new TokenManager(db)));
            var after = await db.TokenAuditLog.CountAsync();
            after.Should().Be(before);
        }
    }

    [Fact]
    public async Task InternalService_with_Guid_id_writes_one_audit_row()
    {
        var (factory, db) = await NewFactoryAndDb();
        await using (factory)
        {
            var tokenId = Guid.NewGuid();
            // Seed a token so the FK holds.
            db.ApiTokens.Add(new Waypoint.Domain.Entities.ApiToken
            {
                Id = tokenId,
                Name = "audit-token",
                Prefix = "abcdef01",
                TokenHash = "$argon2id$v=19$m=65536,t=3,p=1$" + Convert.ToBase64String(new byte[16]) + "$" + Convert.ToBase64String(new byte[32]),
                Scopes = [],
            });
            await db.SaveChangesAsync();

            var before = await db.TokenAuditLog.CountAsync(a => a.TokenId == tokenId);
            var mw = new AuditLogMiddleware(_ => Task.CompletedTask);
            var svc = new Principal(PrincipalKind.InternalService, tokenId.ToString(), "audit-token", []);
            await mw.InvokeAsync(WithPrincipal(svc, "POST", "/api/v1/foo"), new TokenService(new TokenManager(db)));
            var after = await db.TokenAuditLog.CountAsync(a => a.TokenId == tokenId);
            after.Should().Be(before + 1);
        }
    }

    [Fact]
    public async Task Audit_row_captures_method_path_status_code()
    {
        var (factory, db) = await NewFactoryAndDb();
        await using (factory)
        {
            var tokenId = Guid.NewGuid();
            db.ApiTokens.Add(new Waypoint.Domain.Entities.ApiToken
            {
                Id = tokenId,
                Name = "t",
                Prefix = "12345678",
                TokenHash = "$argon2id$v=19$m=65536,t=3,p=1$" + Convert.ToBase64String(new byte[16]) + "$" + Convert.ToBase64String(new byte[32]),
                Scopes = [],
            });
            await db.SaveChangesAsync();

            var mw = new AuditLogMiddleware(c => { c.Response.StatusCode = 418; return Task.CompletedTask; });
            var svc = new Principal(PrincipalKind.InternalService, tokenId.ToString(), "t", []);
            var ctx = WithPrincipal(svc, "DELETE", "/internal/v1/foo");
            await mw.InvokeAsync(ctx, new TokenService(new TokenManager(db)));

            var row = await db.TokenAuditLog.OrderByDescending(a => a.At)
                .FirstAsync(a => a.TokenId == tokenId);
            row.Method.Should().Be("DELETE");
            row.Path.Should().Be("/internal/v1/foo");
            row.StatusCode.Should().Be(418);
        }
    }

    [Fact]
    public async Task Audit_row_captures_passthrough_actor_when_set_on_principal()
    {
        var (factory, db) = await NewFactoryAndDb();
        await using (factory)
        {
            var tokenId = Guid.NewGuid();
            db.ApiTokens.Add(new Waypoint.Domain.Entities.ApiToken
            {
                Id = tokenId,
                Name = "p",
                Prefix = "abcdefab",
                TokenHash = "$argon2id$v=19$m=65536,t=3,p=1$" + Convert.ToBase64String(new byte[16]) + "$" + Convert.ToBase64String(new byte[32]),
                Scopes = [],
            });
            await db.SaveChangesAsync();

            var mw = new AuditLogMiddleware(_ => Task.CompletedTask);
            var svc = new Principal(PrincipalKind.InternalService, tokenId.ToString(), "p", [],
                PassthroughActorId: "ag-1", PassthroughActorLabel: "Agent 1");
            await mw.InvokeAsync(WithPrincipal(svc), new TokenService(new TokenManager(db)));

            var row = await db.TokenAuditLog.OrderByDescending(a => a.At)
                .FirstAsync(a => a.TokenId == tokenId);
            row.PassthroughActorId.Should().Be("ag-1");
            row.PassthroughActorLabel.Should().Be("Agent 1");
        }
    }
}
