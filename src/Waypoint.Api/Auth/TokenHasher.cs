using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace Waypoint.Api.Auth;

/// <summary>
/// Argon2id-based token hashing. Stored format: $argon2id$v=19$m=65536,t=3,p=1$<salt-b64>$<hash-b64>.
/// Salt is 16 random bytes; output hash is 32 bytes. Parameters tuned for ~50ms verification on
/// commodity hardware — fast enough for the per-request internal path, slow enough to defang
/// brute force on a leaked DB.
/// </summary>
public static class TokenHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int MemoryKb = 65536;
    private const int Iterations = 3;
    private const int Parallelism = 1;

    public static string Hash(string secret)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Argon2id(secret, salt);
        return $"$argon2id$v=19$m={MemoryKb},t={Iterations},p={Parallelism}$" +
               $"{Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string secret, string stored)
    {
        var parts = stored.Split('$');
        if (parts.Length < 6) return false;
        var salt = Convert.FromBase64String(parts[4]);
        var expected = Convert.FromBase64String(parts[5]);
        var actual = Argon2id(secret, salt);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private static byte[] Argon2id(string secret, byte[] salt)
    {
        using var hasher = new Argon2id(Encoding.UTF8.GetBytes(secret))
        {
            Salt = salt,
            DegreeOfParallelism = Parallelism,
            MemorySize = MemoryKb,
            Iterations = Iterations,
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
