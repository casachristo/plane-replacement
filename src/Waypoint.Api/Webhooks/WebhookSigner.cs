using System.Security.Cryptography;
using System.Text;

namespace Waypoint.Api.Webhooks;

public static class WebhookSigner
{
    /// <summary>Returns "sha256=&lt;hex&gt;" suitable for the X-Waypoint-Signature header.</summary>
    public static string Sign(string secret, string body)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        return "sha256=" + Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
