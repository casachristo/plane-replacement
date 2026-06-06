using FluentAssertions;
using Waypoint.Api.Auth;
using Xunit;

namespace Waypoint.Api.Tests.Auth;

/// <summary>
/// Mutation-coverage tests for TokenHasher. The class is small and
/// security-critical; these pin the contract (round-trip verify, salt
/// uniqueness, wrong-secret rejection, format of GenerateNew output).
/// </summary>
public class TokenHasherMutationCoverage
{
    [Fact]
    public void Verify_returns_true_for_round_tripped_secret()
    {
        const string secret = "my-secret-value";
        var hash = TokenHasher.Hash(secret);
        TokenHasher.Verify(secret, hash).Should().BeTrue();
    }

    [Fact]
    public void Verify_returns_false_for_wrong_secret()
    {
        var hash = TokenHasher.Hash("correct");
        TokenHasher.Verify("wrong", hash).Should().BeFalse();
    }

    [Fact]
    public void Hash_produces_distinct_hashes_for_same_input_due_to_random_salt()
    {
        var a = TokenHasher.Hash("same");
        var b = TokenHasher.Hash("same");
        a.Should().NotBe(b);
    }

    [Fact]
    public void Verify_returns_false_for_garbage_stored_hash()
    {
        TokenHasher.Verify("any", "not-a-valid-hash-string").Should().BeFalse();
    }

    [Fact]
    public void GenerateNew_returns_prefix_of_length_8()
    {
        var (prefix, _) = TokenHasher.GenerateNew();
        prefix.Should().HaveLength(8);
    }

    [Fact]
    public void GenerateNew_full_token_starts_with_wpt_underscore_prefix()
    {
        var (prefix, full) = TokenHasher.GenerateNew();
        full.Should().StartWith($"wpt_{prefix}_");
    }

    [Fact]
    public void GenerateNew_distinct_calls_return_distinct_full_tokens()
    {
        var (_, full1) = TokenHasher.GenerateNew();
        var (_, full2) = TokenHasher.GenerateNew();
        full1.Should().NotBe(full2);
    }

    [Fact]
    public void GenerateNew_full_token_has_three_underscore_parts()
    {
        var (_, full) = TokenHasher.GenerateNew();
        full.Split('_').Length.Should().Be(3);
    }
}
