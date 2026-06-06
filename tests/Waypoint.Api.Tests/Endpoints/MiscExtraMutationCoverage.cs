using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Waypoint.Api.Auth;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Waypoint.Domain;
using Waypoint.Domain.Entities;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

public class ProjectEndpointsExtra2MutationCoverage : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public ProjectEndpointsExtra2MutationCoverage(PostgresFixture pg) => _pg = pg;

    [Fact]
    public async Task POST_project_returned_DTO_CreatedAt_is_recent()
    {
        await using var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var dto = await (await c.PostAsJsonAsync("/api/v1/projects",
            new CreateProjectRequest("pxc1", "P", "PXC1"))).Content.ReadFromJsonAsync<ProjectDto>();
        dto!.CreatedAt.Should().BeAfter(DateTimeOffset.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task POST_project_returned_DTO_UpdatedAt_is_recent()
    {
        await using var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var dto = await (await c.PostAsJsonAsync("/api/v1/projects",
            new CreateProjectRequest("pxc2", "P", "PXC2"))).Content.ReadFromJsonAsync<ProjectDto>();
        dto!.UpdatedAt.Should().BeAfter(DateTimeOffset.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task GET_unknown_project_returns_404_with_envelope_message()
    {
        await using var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        using var c = f.CreateClient();
        var resp = await c.GetAsync("/api/v1/projects/no-such-slug-zzz");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Error.Message.Should().Contain("no-such-slug-zzz");
    }
}

public class OidcSessionResolverExtraMutationCoverage : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public OidcSessionResolverExtraMutationCoverage(PostgresFixture pg) => _pg = pg;

    [Fact]
    public async Task Resolve_with_empty_cookie_string_returns_null()
    {
        var f = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await f.EnsureMigratedAsync();
        await using var _ = f;
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WaypointDbContext>();
        var resolver = new OidcSessionResolver(db, Microsoft.Extensions.Options.Options.Create(new OidcOptions()));
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers.Cookie = OidcSessionResolver.CookieName + "=";
        var p = await resolver.ResolveAsync(ctx, CancellationToken.None);
        p.Should().BeNull();
    }

    [Fact]
    public void HashCookie_returns_uppercase_hex()
    {
        var hex = OidcSessionResolver.HashCookie("abc");
        hex.Should().MatchRegex("^[0-9A-F]+$");
    }

    [Fact]
    public void HashCookie_returns_64_hex_chars_for_SHA256_output()
    {
        var hex = OidcSessionResolver.HashCookie("abc");
        hex.Length.Should().Be(64);
    }
}

public class ServiceBearerResolverExtra2MutationCoverage : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public ServiceBearerResolverExtra2MutationCoverage(PostgresFixture pg) => _pg = pg;

    private async Task<(WaypointApiFactory factory, string fullToken, string prefix)>
        Mint(Waypoint.Domain.Enums.TokenKind kind, string[] scopes)
    {
        var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        var (prefix, full) = TokenHasher.GenerateNew();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WaypointDbContext>();
        db.ApiTokens.Add(new ApiToken
        {
            Name = "t-" + prefix,
            Prefix = prefix,
            TokenHash = TokenHasher.Hash(full),
            Scopes = scopes,
            Kind = kind,
        });
        await db.SaveChangesAsync();
        return (factory, full, prefix);
    }

    [Fact]
    public async Task Resolved_principal_carries_token_Name_as_DisplayName()
    {
        var (factory, full, _) = await Mint(Waypoint.Domain.Enums.TokenKind.Service, ["read"]);
        await using (factory)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WaypointDbContext>();
            var resolver = new ServiceBearerResolver(db);
            var ctx = new DefaultHttpContext();
            ctx.Request.Headers.Authorization = $"Bearer {full}";
            var p = await resolver.ResolveAsync(ctx, CancellationToken.None);
            p!.DisplayName.Should().StartWith("t-");
        }
    }

    [Fact]
    public async Task Resolved_principal_Kind_is_InternalService()
    {
        var (factory, full, _) = await Mint(Waypoint.Domain.Enums.TokenKind.Service, []);
        await using (factory)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WaypointDbContext>();
            var resolver = new ServiceBearerResolver(db);
            var ctx = new DefaultHttpContext();
            ctx.Request.Headers.Authorization = $"Bearer {full}";
            var p = await resolver.ResolveAsync(ctx, CancellationToken.None);
            p!.Kind.Should().Be(PrincipalKind.InternalService);
        }
    }

    [Fact]
    public async Task Resolved_principal_Id_is_token_database_Id_not_prefix()
    {
        var (factory, full, prefix) = await Mint(Waypoint.Domain.Enums.TokenKind.Service, []);
        await using (factory)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WaypointDbContext>();
            var resolver = new ServiceBearerResolver(db);
            var ctx = new DefaultHttpContext();
            ctx.Request.Headers.Authorization = $"Bearer {full}";
            var p = await resolver.ResolveAsync(ctx, CancellationToken.None);
            p!.Id.Should().NotBe(prefix);
            Guid.TryParse(p.Id, out _).Should().BeTrue();
        }
    }
}
