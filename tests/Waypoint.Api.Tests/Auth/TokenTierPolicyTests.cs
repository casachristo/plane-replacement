using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Waypoint.Api.Auth;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Waypoint.Domain;
using Waypoint.Domain.Entities;
using Waypoint.Domain.Enums;
using Xunit;

namespace Waypoint.Api.Tests.Auth;

/// <summary>
/// WAY-5: tier policy at the HTTP boundary — project creation is admin-only, and every
/// service-token call records its tier in the audit log.
/// </summary>
public class TokenTierPolicyTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public TokenTierPolicyTests(PostgresFixture pg) => _pg = pg;

    [Fact]
    public async Task Admin_scoped_principal_can_create_a_project()
    {
        // Default TestPrincipal is a human WITH the admin scope.
        var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        await using (factory)
        {
            using var client = factory.CreateClient();
            var resp = await client.PostAsJsonAsync("/api/v1/projects",
                new CreateProjectRequest("tier-admin", "Tier Admin", "TA"));
            resp.StatusCode.Should().Be(HttpStatusCode.Created);
        }
    }

    [Fact]
    public async Task Non_admin_principal_cannot_create_a_project()
    {
        // A limited principal (no admin scope) must be refused project creation.
        var factory = new WaypointApiFactory
        {
            PostgresConnectionString = _pg.ConnectionString,
            TestPrincipal = new Principal(
                Kind: PrincipalKind.Human,
                Id: "22222222-2222-2222-2222-222222222222",
                DisplayName: "Limited",
                Scopes: ["issue:read", "issue:write"]),
        };
        await factory.EnsureMigratedAsync();
        await using (factory)
        {
            using var client = factory.CreateClient();
            var resp = await client.PostAsJsonAsync("/api/v1/projects",
                new CreateProjectRequest("tier-nope", "Tier Nope", "TN"));
            ((int)resp.StatusCode).Should().Be(422);
            var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
            err!.Error.Code.Should().Be("missing_scope");
        }
    }

    [Fact]
    public async Task Service_token_call_records_its_tier_in_the_audit_log()
    {
        // Mint a real Service token row, then drive a request as an InternalService principal
        // bound to that token id; the audit middleware must persist the tier (WAY-5).
        var tokenId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var factory = new WaypointApiFactory
        {
            PostgresConnectionString = _pg.ConnectionString,
            TestPrincipal = new Principal(
                Kind: PrincipalKind.InternalService,
                Id: tokenId.ToString(),
                DisplayName: "limited-writer",
                Scopes: ["issue:read"],
                TokenKind: TokenKind.Service),
        };
        await factory.EnsureMigratedAsync();
        await using (factory)
        {
            using (var scope = factory.Services.CreateScope())
            {
                var seed = scope.ServiceProvider.GetRequiredService<WaypointDbContext>();
                seed.ApiTokens.Add(new ApiToken
                {
                    Id = tokenId,
                    Name = "limited-writer",
                    Prefix = "auditknd",
                    TokenHash = "x",
                    Scopes = ["issue:read"],
                    Kind = TokenKind.Service,
                });
                await seed.SaveChangesAsync();
            }

            using var client = factory.CreateClient();
            (await client.GetAsync("/api/v1/projects")).StatusCode.Should().Be(HttpStatusCode.OK);

            using var verify = factory.Services.CreateScope();
            var db = verify.ServiceProvider.GetRequiredService<WaypointDbContext>();
            var rows = db.TokenAuditLog.Where(a => a.TokenId == tokenId).ToList();
            rows.Should().ContainSingle();
            rows[0].TokenKind.Should().Be(TokenKind.Service);
        }
    }
}
