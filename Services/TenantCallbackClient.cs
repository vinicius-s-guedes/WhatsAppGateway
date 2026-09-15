using System.Text;
using System.Text.Json;
using WhatsAppGateway.Models;

namespace WhatsAppGateway.Services;

public sealed class TenantCallbackClient(HttpClient httpClient)
{
    public async Task SendAsync(string callbackUrl, string? callbackSecret, InboundCallback payload, CancellationToken ct)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var request = new HttpRequestMessage(HttpMethod.Post, callbackUrl)
        {
            Content = new ByteArrayContent(bytes)
        };
        request.Content.Headers.ContentType = new("application/json");
        if (!string.IsNullOrWhiteSpace(callbackSecret))
            request.Headers.TryAddWithoutValidation("X-Gateway-Signature-256", WebhookSecurity.Sign(bytes, callbackSecret));

        using var response = await httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Tenant callback returned HTTP {(int)response.StatusCode}.");
    }
}
