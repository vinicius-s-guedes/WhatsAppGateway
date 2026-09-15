using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Npgsql;
using WhatsAppGateway.Data;
using WhatsAppGateway.Infrastructure;
using WhatsAppGateway.Models;

namespace WhatsAppGateway.Controllers;

[ApiController]
[Route("api/admin")]
public sealed class AdminController(
    GatewayRepository repository,
    SecretProtector protector,
    IOptions<GatewayOptions> options) : ControllerBase
{
    [HttpGet("tenants")]
    public async Task<IActionResult> ListTenants()
    {
        var tenants = await repository.ListTenantsAsync();
        return Ok(tenants.Select(x => new
        {
            x.Id, x.Name, x.CallbackUrl, x.Active, x.CreatedAt
        }));
    }

    [HttpPost("tenants")]
    public async Task<IActionResult> CreateTenant(TenantCreateRequest input)
    {
        if (string.IsNullOrWhiteSpace(input.Name)) return BadRequest(new { error = "name_required" });
        if (!IsValidHttpsUrl(input.CallbackUrl)) return BadRequest(new { error = "callback_url_must_be_https" });

        var tenant = await repository.CreateTenantAsync(new TenantRecord(
            Guid.NewGuid(), input.Name.Trim(), EmptyToNull(input.CallbackUrl),
            string.IsNullOrWhiteSpace(input.CallbackSecret) ? null : protector.Protect(input.CallbackSecret),
            true, DateTimeOffset.UtcNow));
        return Created($"/api/admin/tenants/{tenant.Id}", new
        {
            tenant.Id, tenant.Name, tenant.CallbackUrl, tenant.Active, tenant.CreatedAt
        });
    }

    [HttpGet("apps")]
    public async Task<IActionResult> ListApps()
    {
        var apps = await repository.ListAppsAsync();
        return Ok(apps.Select(ToAppResponse));
    }

    [HttpPost("apps")]
    public async Task<IActionResult> CreateApp(WhatsAppAppCreateRequest input)
    {
        if (new[] { input.Name, input.AppSecret, input.VerifyToken }.Any(string.IsNullOrWhiteSpace))
            return BadRequest(new { error = "required_app_field_missing" });
        var app = await repository.CreateAppAsync(new WhatsAppAppRecord(
            Guid.NewGuid(), input.Name.Trim(), EmptyToNull(input.MetaAppId),
            protector.Protect(input.AppSecret.Trim()), protector.Protect(input.VerifyToken.Trim()),
            Guid.NewGuid(), true, DateTimeOffset.UtcNow));
        return Created($"/api/admin/apps/{app.Id}", ToAppResponse(app));
    }

    [HttpGet("tenants/{tenantId:guid}/channels")]
    public async Task<IActionResult> ListChannels(Guid tenantId)
    {
        var channels = await repository.ListChannelsAsync(tenantId);
        return Ok(channels.Select(ToChannelResponse));
    }

    [HttpPost("tenants/{tenantId:guid}/channels")]
    public async Task<IActionResult> CreateChannel(Guid tenantId, ChannelCreateRequest input)
    {
        if (await repository.GetTenantAsync(tenantId) is null) return NotFound(new { error = "tenant_not_found" });
        if (await repository.GetAppAsync(input.AppId) is null) return BadRequest(new { error = "whatsapp_app_not_found" });
        if (new[] { input.Name, input.WabaId, input.PhoneNumberId, input.AccessToken }
            .Any(string.IsNullOrWhiteSpace)) return BadRequest(new { error = "required_channel_field_missing" });

        var version = string.IsNullOrWhiteSpace(input.GraphApiVersion)
            ? options.Value.DefaultGraphApiVersion
            : input.GraphApiVersion.Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(version, "^v[0-9]+\\.[0-9]+$"))
            return BadRequest(new { error = "invalid_graph_api_version" });

        var channel = new ChannelRecord(
            Guid.NewGuid(), tenantId, input.AppId, input.Name.Trim(), input.WabaId.Trim(), input.PhoneNumberId.Trim(),
            EmptyToNull(input.DisplayPhoneNumber), protector.Protect(input.AccessToken.Trim()),
            version, true, DateTimeOffset.UtcNow);
        try
        {
            channel = await repository.CreateChannelAsync(channel);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return Conflict(new { error = "phone_number_id_already_registered" });
        }

        return Created($"/api/admin/tenants/{tenantId}/channels/{channel.Id}", ToChannelResponse(channel));
    }

    private object ToChannelResponse(ChannelRecord channel) => new
    {
        channel.Id,
        channel.TenantId,
        channel.AppId,
        channel.Name,
        channel.WabaId,
        channel.PhoneNumberId,
        channel.DisplayPhoneNumber,
        channel.GraphApiVersion,
        channel.Active,
        channel.CreatedAt
    };

    private object ToAppResponse(WhatsAppAppRecord app) => new
    {
        app.Id,
        app.Name,
        app.MetaAppId,
        app.Active,
        app.CreatedAt,
        webhookUrl = $"{options.Value.PublicBaseUrl.TrimEnd('/')}/api/webhooks/whatsapp/{app.WebhookKey}"
    };

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsValidHttpsUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
    }
}
