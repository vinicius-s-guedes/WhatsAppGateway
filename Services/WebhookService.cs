using System.Globalization;
using System.Text.Json;
using WhatsAppGateway.Data;
using WhatsAppGateway.Infrastructure;
using WhatsAppGateway.Models;

namespace WhatsAppGateway.Services;

public sealed class WebhookService(
    GatewayRepository repository,
    TenantCallbackClient callbacks,
    SecretProtector protector,
    ILogger<WebhookService> logger)
{
    public async Task ProcessAsync(ChannelRecord channel, MetaWebhookEnvelope envelope, string rawPayload, CancellationToken ct)
    {
        var tenant = await repository.GetTenantAsync(channel.TenantId);
        if (tenant is null || !tenant.Active) return;

        foreach (var change in envelope.Entry.SelectMany(x => x.Changes))
        {
            if (!string.Equals(change.Value.Metadata?.PhoneNumberId, channel.PhoneNumberId, StringComparison.Ordinal))
            {
                logger.LogWarning("Ignoring webhook whose phone_number_id does not match channel {ChannelId}", channel.Id);
                continue;
            }

            foreach (var message in change.Value.Messages)
            {
                var eventKey = message.Id;
                if (string.IsNullOrWhiteSpace(eventKey) || !await repository.TryAddEventAsync(
                        channel.Id, eventKey, "message", message.From, message.Text?.Body, rawPayload)) continue;

                var contact = change.Value.Contacts.FirstOrDefault(x => x.WaId == message.From);
                var callback = new InboundCallback(
                    tenant.Id, channel.Id, channel.PhoneNumberId, eventKey, "message", message.From,
                    contact?.Profile?.Name, message.Type, message.Text?.Body, ParseTimestamp(message.Timestamp));
                await DeliverAsync(tenant, channel.Id, eventKey, callback, ct);
            }

            foreach (var status in change.Value.Statuses)
            {
                var eventKey = $"{status.Id}:{status.Status}:{status.Timestamp}";
                if (!await repository.TryAddEventAsync(channel.Id, eventKey, "status", status.RecipientId, null, rawPayload)) continue;
                var callback = new InboundCallback(
                    tenant.Id, channel.Id, channel.PhoneNumberId, status.Id, "status", status.RecipientId,
                    null, status.Status, null, ParseTimestamp(status.Timestamp));
                await DeliverAsync(tenant, channel.Id, eventKey, callback, ct);
            }
        }
    }

    private async Task DeliverAsync(TenantRecord tenant, Guid channelId, string eventKey, InboundCallback callback, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tenant.CallbackUrl))
        {
            await repository.SetCallbackResultAsync(channelId, eventKey, true, null);
            return;
        }

        try
        {
            var secret = string.IsNullOrWhiteSpace(tenant.CallbackSecretCipher)
                ? null
                : protector.Unprotect(tenant.CallbackSecretCipher);
            await callbacks.SendAsync(tenant.CallbackUrl, secret, callback, ct);
            await repository.SetCallbackResultAsync(channelId, eventKey, true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to deliver event {EventKey} for tenant {TenantId}", eventKey, tenant.Id);
            await repository.SetCallbackResultAsync(channelId, eventKey, false, ex.Message);
        }
    }

    private static DateTimeOffset ParseTimestamp(string? value) =>
        long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : DateTimeOffset.UtcNow;
}
