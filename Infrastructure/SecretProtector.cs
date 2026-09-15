using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace WhatsAppGateway.Infrastructure;

public sealed class SecretProtector
{
    private readonly byte[] _key;

    public SecretProtector(IOptions<GatewayOptions> options)
    {
        try
        {
            _key = Convert.FromBase64String(options.Value.EncryptionKey);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("Gateway:EncryptionKey must be a Base64 value containing 32 random bytes.");
        }

        if (_key.Length != 32)
            throw new InvalidOperationException("Gateway:EncryptionKey must contain exactly 32 bytes.");
    }

    public string Protect(string plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var input = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[input.Length];
        using var aes = new AesGcm(_key, tag.Length);
        aes.Encrypt(nonce, input, ciphertext, tag);
        return Convert.ToBase64String([.. nonce, .. tag, .. ciphertext]);
    }

    public string Unprotect(string protectedValue)
    {
        var packed = Convert.FromBase64String(protectedValue);
        if (packed.Length < 28) throw new CryptographicException("Invalid protected value.");
        var nonce = packed.AsSpan(0, 12);
        var tag = packed.AsSpan(12, 16);
        var ciphertext = packed.AsSpan(28);
        var plaintext = new byte[ciphertext.Length];
        using var aes = new AesGcm(_key, tag.Length);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);
        return Encoding.UTF8.GetString(plaintext);
    }
}
