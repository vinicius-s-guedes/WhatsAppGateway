using Microsoft.AspNetCore.Mvc;
using WhatsAppGateway.Data;
using WhatsAppGateway.Models;
using WhatsAppGateway.Services;

namespace WhatsAppGateway.Controllers;

[ApiController]
[Route("api/messages")]
public sealed class MessagesController(GatewayRepository repository, MetaWhatsAppClient meta) : ControllerBase
{
    [HttpPost("media")]
    [RequestSizeLimit(104_857_600)]
    public async Task<IActionResult> UploadMedia(
        [FromForm] Guid tenantId,
        [FromForm] Guid channelId,
        [FromForm] IFormFile file,
        CancellationToken ct)
    {
        var channel = await repository.GetChannelAsync(tenantId, channelId);
        if (channel is null) return NotFound(new { error = "active_channel_not_found" });
        if (file.Length == 0) return BadRequest(new { error = "empty_file" });
        try
        {
            await using var stream = file.OpenReadStream();
            return Ok(await meta.UploadMediaAsync(
                channel, stream, file.FileName,
                string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType, ct));
        }
        catch (MetaApiException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                error = "meta_api_error", metaStatus = ex.StatusCode, metaResponse = ex.ResponseBody
            });
        }
    }

    [HttpPost("text")]
    public Task<IActionResult> SendText(TextMessageRequest input, CancellationToken ct) =>
        Send(input.TenantId, input.ChannelId, channel => meta.SendTextAsync(channel, input, ct));

    [HttpPost("template")]
    public Task<IActionResult> SendTemplate(TemplateMessageRequest input, CancellationToken ct) =>
        Send(input.TenantId, input.ChannelId, channel => meta.SendTemplateAsync(channel, input, ct));

    [HttpPost("template-document")]
    public Task<IActionResult> SendTemplateDocument(TemplateDocumentMessageRequest input, CancellationToken ct) =>
        Send(input.TenantId, input.ChannelId, channel => meta.SendTemplateDocumentAsync(channel, input, ct));

    private async Task<IActionResult> Send(Guid tenantId, Guid channelId, Func<ChannelRecord, Task<System.Text.Json.JsonElement>> action)
    {
        var channel = await repository.GetChannelAsync(tenantId, channelId);
        if (channel is null) return NotFound(new { error = "active_channel_not_found" });
        try
        {
            return Ok(await action(channel));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (MetaApiException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                error = "meta_api_error",
                metaStatus = ex.StatusCode,
                metaResponse = ex.ResponseBody
            });
        }
    }
}
