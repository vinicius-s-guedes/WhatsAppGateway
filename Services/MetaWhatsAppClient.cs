using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using WhatsAppGateway.Infrastructure;
using WhatsAppGateway.Models;

namespace WhatsAppGateway.Services;

public sealed class MetaWhatsAppClient(HttpClient httpClient, SecretProtector protector)
{
    public async Task<JsonElement> UploadMediaAsync(ChannelRecord channel, Stream file, string filename, string contentType, CancellationToken ct)
    {
        var url = $"https://graph.facebook.com/{channel.GraphApiVersion}/{channel.PhoneNumberId}/media";
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", protector.Unprotect(channel.AccessTokenCipher));
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("whatsapp"), "messaging_product");
        var fileContent = new StreamContent(file);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", filename);
        request.Content = form;
        using var response = await httpClient.SendAsync(request, ct);
        var content = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new MetaApiException((int)response.StatusCode, content);
        return JsonSerializer.Deserialize<JsonElement>(content);
    }

    public Task<JsonElement> SendTextAsync(ChannelRecord channel, TextMessageRequest input, CancellationToken ct) =>
        SendAsync(channel, new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = NormalizePhone(input.To),
            type = "text",
            text = new { preview_url = false, body = input.Text }
        }, ct);

    public Task<JsonElement> SendTemplateAsync(ChannelRecord channel, TemplateMessageRequest input, CancellationToken ct) =>
        SendAsync(channel, BuildTemplate(input.To, input.TemplateName, input.LanguageCode, null, input.BodyParameters), ct);

    public Task<JsonElement> SendTemplateDocumentAsync(ChannelRecord channel, TemplateDocumentMessageRequest input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.DocumentUrl) == string.IsNullOrWhiteSpace(input.MediaId))
            throw new ArgumentException("Supply exactly one of documentUrl or mediaId.");

        object document = !string.IsNullOrWhiteSpace(input.MediaId)
            ? new { id = input.MediaId, filename = input.Filename }
            : new { link = input.DocumentUrl, filename = input.Filename };

        return SendAsync(channel, BuildTemplate(
            input.To, input.TemplateName, input.LanguageCode,
            new { type = "document", document }, input.BodyParameters), ct);
    }

    private static object BuildTemplate(
        string to,
        string templateName,
        string languageCode,
        object? headerParameter,
        IReadOnlyList<TemplateParameter>? bodyParameters)
    {
        var components = new List<object>();
        if (headerParameter is not null)
            components.Add(new { type = "header", parameters = new[] { headerParameter } });
        if (bodyParameters is { Count: > 0 })
            components.Add(new
            {
                type = "body",
                parameters = bodyParameters.Select(x => new { type = x.Type, text = x.Text }).ToArray()
            });

        return new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = NormalizePhone(to),
            type = "template",
            template = new { name = templateName, language = new { code = languageCode }, components }
        };
    }

    private async Task<JsonElement> SendAsync(ChannelRecord channel, object payload, CancellationToken ct)
    {
        var url = $"https://graph.facebook.com/{channel.GraphApiVersion}/{channel.PhoneNumberId}/messages";
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", protector.Unprotect(channel.AccessTokenCipher));
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await httpClient.SendAsync(request, ct);
        var content = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new MetaApiException((int)response.StatusCode, content);
        return JsonSerializer.Deserialize<JsonElement>(content);
    }

    private static string NormalizePhone(string value) => new(value.Where(char.IsDigit).ToArray());
}

public sealed class MetaApiException(int statusCode, string responseBody)
    : Exception($"Meta API returned HTTP {statusCode}: {responseBody}")
{
    public int StatusCode { get; } = statusCode;
    public string ResponseBody { get; } = responseBody;
}
