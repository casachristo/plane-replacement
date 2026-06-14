using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace Waypoint.Api.Auth;

/// <summary>
/// Argon2id-based token hashing. Stored format: $argon2id$v=19$m=65536,t=3,p=1$&lt;salt-b64&gt;$&lt;hash-b64&gt;.
/// Salt is 16 random bytes; output hash is 32 bytes. Production parameters are tuned for ~50ms
/// verification on commodity hardware — fast enough for the per-request internal path, slow enough
/// to defang brute force on a leaked DB.
///
/// WAY-25: the Hash cost is configurable via WAYPOINT_ARGON2_MEMORY_KB / WAYPOINT_ARGON2_ITERATIONS
/// (defaults = the strong production values). The CI mutation jobs set cheap values so the 64MB/3-iter
/// cost stops dominating Stryker runs — that memory/timing pressure made the api-auth mutation score
/// non-deterministic (the "ServiceBearerResolver null flake"). Verify reads the cost parameters
/// embedded in each stored hash (PHC format), so a token hashed at any cost still verifies regardless
/// of the current configured Hash cost.
/// </summary>
public static class TokenHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Parallelism = 1;
    private static readonly int MemoryKb = EnvInt("WAYPOINT_ARGON2_MEMORY_KB", 65536);
    private static readonly int Iterations = EnvInt("WAYPOINT_ARGON2_ITERATIONS", 3);

    private static int EnvInt(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out var v) && v > 0 ? v : fallback;

    public static string Hash(string secret)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Argon2id(secret, salt, MemoryKb, Iterations);
        return $"$argon2id$v=19$m={MemoryKb},t={Iterations},p={Parallelism}$" +
               $"{Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string secret, string stored)
    {
        var parts = stored.Split('$');
        if (parts.Length < 6) return false;
        // Use the cost parameters embedded in the stored hash (PHC format) rather than the
        // current static cost, so verification is correct no matter what Hash cost is configured
        // — e.g. a prod-cost token still verifies inside a cheap-cost test process.
        if (!TryParseCost(parts[3], out var memoryKb, out var iterations)) return false;
        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[4]);
            expected = Convert.FromBase64String(parts[5]);
        }
        catch (FormatException) { return false; }
        var actual = Argon2id(secret, salt, memoryKb, iterations);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    // Parses the "m=65536,t=3,p=1" PHC parameter segment. Returns false if m or t is absent.
    private static bool TryParseCost(string segment, out int memoryKb, out int iterations)
    {
        memoryKb = 0;
        iterations = 0;
        foreach (var kv in segment.Split(','))
        {
            var eq = kv.Split('=');
            if (eq.Length != 2) continue;
            if (eq[0] == "m" && int.TryParse(eq[1], out var m)) memoryKb = m;
            else if (eq[0] == "t" && int.TryParse(eq[1], out var t)) iterations = t;
        }
        return memoryKb > 0 && iterations > 0;
    }

    private static byte[] Argon2id(string secret, byte[] salt, int memoryKb, int iterations)
    {
        using var hasher = new Argon2id(Encoding.UTF8.GetBytes(secret))
        {
            Salt = salt,
            DegreeOfParallelism = Parallelism,
            MemorySize = memoryKb,
            Iterations = iterations,
        };
        return hasher.GetBytes(HashSize);
    }

    public static (string Prefix, string FullToken) GenerateNew()
    {
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var prefix = secret[..8];
        return (prefix, $"wpt_{prefix}_{secret[8..]}");
    }
}
