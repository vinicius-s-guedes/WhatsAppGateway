using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using WhatsAppGateway.Infrastructure;
using WhatsAppGateway.Services;
using Xunit;

namespace WhatsAppGateway.Tests;

public sealed class SecurityTests
{
    [Fact]
    public void SecretProtector_RoundTripsWithoutExposingPlaintext()
    {
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var protector = new SecretProtector(Options.Create(new GatewayOptions { EncryptionKey = key }));

        var encrypted = protector.Protect("access-token-secret");

        Assert.DoesNotContain("access-token-secret", encrypted);
        Assert.Equal("access-token-secret", protector.Unprotect(encrypted));
    }

    [Fact]
    public void SecretProtector_UsesDifferentNonceForEveryEncryption()
    {
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var protector = new SecretProtector(Options.Create(new GatewayOptions { EncryptionKey = key }));

        Assert.NotEqual(protector.Protect("same"), protector.Protect("same"));
    }

    [Fact]
    public void WebhookSignature_AcceptsValidSignatureAndRejectsChangedBody()
    {
        var body = "{\"message\":\"hello\"}"u8.ToArray();
        var signature = WebhookSecurity.Sign(body, "app-secret");

        Assert.True(WebhookSecurity.VerifySignature(body, signature, "app-secret"));
        Assert.False(WebhookSecurity.VerifySignature("{}"u8, signature, "app-secret"));
    }
}
