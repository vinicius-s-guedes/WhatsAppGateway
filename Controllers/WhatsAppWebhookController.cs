using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WhatsAppGateway.Data;
using WhatsAppGateway.Infrastructure;
using WhatsAppGateway.Models;
using WhatsAppGateway.Services;

namespace WhatsAppGateway.Controllers;

[ApiController]
[Route("api/webhooks/whatsapp/{webhookKey:guid}")]
public sealed class WhatsAppWebhookController(
    GatewayRepository repository,
    SecretProtector protector,
    WebhookService webhookService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Verify(
        Guid webhookKey,
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? verifyToken,
        [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        var app = await repository.GetAppByWebhookKeyAsync(webhookKey);
        if (app is null || mode != "subscribe" || verifyToken is null ||
            !FixedTimeEquals(protector.Unprotect(app.VerifyTokenCipher), verifyToken)) return Forbid();
        return Content(challenge ?? "", "text/plain", Encoding.UTF8);
    }

    [HttpPost]
    public async Task<IActionResult> Receive(Guid webhookKey, CancellationToken ct)
    {
        var app = await repository.GetAppByWebhookKeyAsync(webhookKey);
        if (app is null) return NotFound();

        using var buffer = new MemoryStream();
        await Request.Body.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        if (!WebhookSecurity.VerifySignature(bytes, Request.Headers["X-Hub-Signature-256"],
                protector.Unprotect(app.AppSecretCipher))) return Unauthorized();

        MetaWebhookEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<MetaWebhookEnvelope>(bytes);
        }
        catch (JsonException)
        {
            return BadRequest(new { error = "invalid_json" });
        }

        if (envelope is null) return BadRequest(new { error = "empty_payload" });
        var phoneNumberIds = envelope.Entry.SelectMany(x => x.Changes)
            .Select(x => x.Value.Metadata?.PhoneNumberId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal);
        foreach (var phoneNumberId in phoneNumberIds)
        {
            var channel = await repository.GetChannelByPhoneNumberIdAsync(app.Id, phoneNumberId!);
            if (channel is not null)
                await webhookService.ProcessAsync(channel, envelope, Encoding.UTF8.GetString(bytes), ct);
        }
        return Ok(new { received = true });
    }

    private static bool FixedTimeEquals(string expected, string supplied)
    {
        var left = Encoding.UTF8.GetBytes(expected);
        var right = Encoding.UTF8.GetBytes(supplied);
        return left.Length == right.Length &&
               System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(left, right);
    }
}
