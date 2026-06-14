using FluentAssertions;
using Waypoint.Api.Subsystems.Identity.Scopes;
using Waypoint.Api.Subsystems.Identity.Tokens;
using Waypoint.Domain;
using Waypoint.Domain.Entities;
using Waypoint.Domain.Enums;
using Xunit;

namespace Waypoint.Api.Tests.Subsystems;

// Pure unit tests for the Identity subsystem's policy/parsing logic: the shared ScopePolicy and
// the TokenService bearer-parsing / effective-scope / kind-validation rules. No DB, no HTTP.
public class IdentityServiceTests
{
    // ---- ScopePolicy ---------------------------------------------------------------------

    private static readonly string[] AdminGroups = { "waypoint-admins" };

    [Fact]
    public void ScopePolicy_grants_the_base_human_scopes_without_admin()
    {
        ScopePolicy.ForGroups(new[] { "engineers" }, AdminGroups)
            .Should().BeEquivalentTo(new[] { "issue:read", "issue:create", "issue:write", "issue:transition", "comment:create" });
    }

    [Fact]
    public void ScopePolicy_adds_admin_only_when_a_group_matches_an_admin_group()
    {
        ScopePolicy.ForGroups(new[] { "waypoint-admins" }, AdminGroups).Should().Contain("admin");
        ScopePolicy.ForGroups(new[] { "qa", "devs" }, AdminGroups).Should().NotContain("admin");
        ScopePolicy.ForGroups(null, AdminGroups).Should().NotContain("admin");
    }

    // ---- TokenService.EffectiveScopes ---------------------------------------------------

    private sealed class StubManager : ITokenManager
    {
        public IReadOnlyList<ApiToken> Candidates = Array.Empty<ApiToken>();
        public Task<IReadOnlyList<ApiToken>> FindActiveServiceCandidatesByPrefixAsync(string prefix, CancellationToken ct) => Task.FromResult(Candidates);
        public Task AddAsync(ApiToken token, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<ApiToken>> ListAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<ApiToken>>(Array.Empty<ApiToken>());
        public Task RevokeAsync(Guid id, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<TokenAuditLog>> ListAuditAsync(Guid? tokenId, DateTimeOffset? since, int take, CancellationToken ct) => Task.FromResult<IReadOnlyList<TokenAuditLog>>(Array.Empty<TokenAuditLog>());
        public Task AddAuditAsync(TokenAuditLog entry, CancellationToken ct) => Task.CompletedTask;
    }

    private static ApiToken Token(TokenKind kind, params string[] scopes) =>
        new() { Id = Guid.NewGuid(), Name = "t", Prefix = "abcdefgh", TokenHash = "x", Scopes = scopes, Kind = kind };

    [Fact]
    public void EffectiveScopes_synthesizes_admin_for_admin_tokens_only()
    {
        var svc = new TokenService(new StubManager());
        svc.EffectiveScopes(Token(TokenKind.Admin, "issue:read")).Should().Contain("admin").And.Contain("issue:read");
        svc.EffectiveScopes(Token(TokenKind.Service, "issue:read")).Should().NotContain("admin");
    }

    [Fact]
    public void EffectiveScopes_does_not_duplicate_admin_when_already_present()
    {
        var svc = new TokenService(new StubManager());
        svc.EffectiveScopes(Token(TokenKind.Admin, "admin", "issue:read"))
            .Count(s => s == "admin").Should().Be(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Bearer something-else")]
    [InlineData("Bearer wpt_short")]            // too short / no underscore at index 12
    public async Task VerifyBearerAsync_returns_null_for_malformed_headers(string header)
    {
        var svc = new TokenService(new StubManager());
        (await svc.VerifyBearerAsync(header, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task VerifyBearerAsync_returns_null_when_no_candidate_verifies()
    {
        // Well-formed "wpt_<8>_..." header but the candidate set is empty → no match.
        var svc = new TokenService(new StubManager { Candidates = Array.Empty<ApiToken>() });
        (await svc.VerifyBearerAsync("Bearer wpt_abcdefgh_secretpart", CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_rejects_an_unknown_kind()
    {
        var svc = new TokenService(new StubManager());
        var act = () => svc.CreateAsync("t", new[] { "issue:read" }, "wizard", CancellationToken.None);
        (await act.Should().ThrowAsync<ValidationException>()).Which.Code.Should().Be("invalid_kind");
    }
}
