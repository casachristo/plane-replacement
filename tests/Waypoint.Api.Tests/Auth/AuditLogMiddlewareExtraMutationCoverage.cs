using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Waypoint.Api.Auth;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Domain;
using Waypoint.Domain.Entities;
using Xunit;

namespace Waypoint.Api.Tests.Auth;

public class AuditLogMiddlewareExtraMutationCoverage : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public AuditLogMiddlewareExtraMutationCoverage(PostgresFixture pg) => _pg = pg;

    private async Task<(WaypointApiFactory factory, WaypointDbContext db, Guid tokenId)> Seed()
    {
        var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WaypointDbContext>();
        var tokenId = Guid.NewGuid();
        db.ApiTokens.Add(new ApiToken
        {
            Id = tokenId,
            Name = "audit-extra",
            Prefix = "auex" + tokenId.ToString("N")[..4],
            TokenHash = "$argon2id$v=19$m=65536,t=3,p=1$" + Convert.ToBase64String(new byte[16]) + "$" + Convert.ToBase64String(new byte[32]),
            Scopes = [],
        });
        await db.SaveChangesAsync();
        return (factory, db, tokenId);
    }

    [Fact]
    public async Task Audit_row_captures_Action_field_as_method_space_path()
    {
        var (factory, db, tokenId) = await Seed();
        await using (factory)
        {
            var mw = new AuditLogMiddleware(_ => Task.CompletedTask);
            var ctx = new DefaultHttpContext();
            ctx.Items[PrincipalMiddleware.ItemKey] = new Principal(
                PrincipalKind.InternalService, tokenId.ToString(), "t", []);
            ctx.Request.Method = "POST";
            ctx.Request.Path = "/internal/v1/foo/bar";
            await mw.InvokeAsync(ctx, db);

            var row = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .FirstAsync(db.TokenAuditLog, a => a.TokenId == tokenId);
            row.Action.Should().Be("POST /internal/v1/foo/bar");
        }
    }

    [Fact]
    public async Task Audit_runs_after_next_so_StatusCode_reflects_downstream_outcome()
    {
        var (factory, db, tokenId) = await Seed();
        await using (factory)
        {
            var mw = new AuditLogMiddleware(c => { c.Response.StatusCode = 503; return Task.CompletedTask; });
            var ctx = new DefaultHttpContext();
            ctx.Items[PrincipalMiddleware.ItemKey] = new Principal(
                PrincipalKind.InternalService, tokenId.ToString(), "t", []);
            ctx.Request.Method = "GET";
            ctx.Request.Path = "/internal/v1/health";
            await mw.InvokeAsync(ctx, db);

            var row = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .FirstAsync(db.TokenAuditLog, a => a.TokenId == tokenId);
            row.StatusCode.Should().Be(503);
        }
    }
}
