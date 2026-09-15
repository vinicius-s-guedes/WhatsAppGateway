using System.Security.Cryptography;
using System.Text;

namespace WhatsAppGateway.Services;

public static class WebhookSecurity
{
    public static bool VerifySignature(ReadOnlySpan<byte> body, string? signatureHeader, string appSecret)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader) || !signatureHeader.StartsWith("sha256=")) return false;
        byte[] supplied;
        try { supplied = Convert.FromHexString(signatureHeader[7..]); }
        catch (FormatException) { return false; }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
        var expected = hmac.ComputeHash(body.ToArray());
        return supplied.Length == expected.Length && CryptographicOperations.FixedTimeEquals(supplied, expected);
    }

    public static string Sign(ReadOnlySpan<byte> body, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return $"sha256={Convert.ToHexString(hmac.ComputeHash(body.ToArray())).ToLowerInvariant()}";
    }
}
