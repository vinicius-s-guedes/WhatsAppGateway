using System.Text.Json;
using Dapper;
using WhatsAppGateway.Models;

namespace WhatsAppGateway.Data;

public sealed class GatewayRepository(DbConnectionFactory connections)
{
    public async Task<TenantRecord> CreateTenantAsync(TenantRecord tenant)
    {
        const string sql = """
            insert into tenants (id, name, callback_url, callback_secret_cipher, active, created_at)
            values (@Id, @Name, @CallbackUrl, @CallbackSecretCipher, @Active, @CreatedAt)
            returning *;
            """;
        await using var db = connections.Create();
        return await db.QuerySingleAsync<TenantRecord>(sql, tenant);
    }

    public async Task<IReadOnlyList<TenantRecord>> ListTenantsAsync()
    {
        await using var db = connections.Create();
        return (await db.QueryAsync<TenantRecord>("select * from tenants order by created_at")).AsList();
    }

    public async Task<TenantRecord?> GetTenantAsync(Guid id)
    {
        await using var db = connections.Create();
        return await db.QuerySingleOrDefaultAsync<TenantRecord>("select * from tenants where id = @id", new { id });
    }

    public async Task<WhatsAppAppRecord> CreateAppAsync(WhatsAppAppRecord app)
    {
        const string sql = """
            insert into whatsapp_apps
              (id, name, meta_app_id, app_secret_cipher, verify_token_cipher, webhook_key, active, created_at)
            values
              (@Id, @Name, @MetaAppId, @AppSecretCipher, @VerifyTokenCipher, @WebhookKey, @Active, @CreatedAt)
            returning *;
            """;
        await using var db = connections.Create();
        return await db.QuerySingleAsync<WhatsAppAppRecord>(sql, app);
    }

    public async Task<IReadOnlyList<WhatsAppAppRecord>> ListAppsAsync()
    {
        await using var db = connections.Create();
        return (await db.QueryAsync<WhatsAppAppRecord>("select * from whatsapp_apps order by created_at")).AsList();
    }

    public async Task<WhatsAppAppRecord?> GetAppAsync(Guid id)
    {
        await using var db = connections.Create();
        return await db.QuerySingleOrDefaultAsync<WhatsAppAppRecord>(
            "select * from whatsapp_apps where id = @id and active = true", new { id });
    }

    public async Task<WhatsAppAppRecord?> GetAppByWebhookKeyAsync(Guid webhookKey)
    {
        await using var db = connections.Create();
        return await db.QuerySingleOrDefaultAsync<WhatsAppAppRecord>(
            "select * from whatsapp_apps where webhook_key = @webhookKey and active = true", new { webhookKey });
    }

    public async Task<ChannelRecord> CreateChannelAsync(ChannelRecord channel)
    {
        const string sql = """
            insert into whatsapp_channels
              (id, tenant_id, app_id, name, waba_id, phone_number_id, display_phone_number,
               access_token_cipher, graph_api_version, active, created_at)
            values
              (@Id, @TenantId, @AppId, @Name, @WabaId, @PhoneNumberId, @DisplayPhoneNumber,
               @AccessTokenCipher, @GraphApiVersion, @Active, @CreatedAt)
            returning *;
            """;
        await using var db = connections.Create();
        return await db.QuerySingleAsync<ChannelRecord>(sql, channel);
    }

    public async Task<IReadOnlyList<ChannelRecord>> ListChannelsAsync(Guid tenantId)
    {
        await using var db = connections.Create();
        return (await db.QueryAsync<ChannelRecord>(
            "select * from whatsapp_channels where tenant_id = @tenantId order by created_at", new { tenantId })).AsList();
    }

    public async Task<ChannelRecord?> GetChannelAsync(Guid tenantId, Guid channelId)
    {
        await using var db = connections.Create();
        return await db.QuerySingleOrDefaultAsync<ChannelRecord>(
            "select * from whatsapp_channels where id = @channelId and tenant_id = @tenantId and active = true",
            new { tenantId, channelId });
    }

    public async Task<ChannelRecord?> GetChannelByPhoneNumberIdAsync(Guid appId, string phoneNumberId)
    {
        await using var db = connections.Create();
        return await db.QuerySingleOrDefaultAsync<ChannelRecord>(
            "select * from whatsapp_channels where app_id = @appId and phone_number_id = @phoneNumberId and active = true",
            new { appId, phoneNumberId });
    }

    public async Task<bool> TryAddEventAsync(
        Guid channelId,
        string eventKey,
        string eventType,
        string? senderPhone,
        string? messageText,
        string rawPayload)
    {
        const string sql = """
            insert into webhook_events
              (id, channel_id, event_key, event_type, sender_phone, message_text, payload)
            values
              (@id, @channelId, @eventKey, @eventType, @senderPhone, @messageText, cast(@rawPayload as jsonb))
            on conflict (channel_id, event_key) do nothing;
            """;
        await using var db = connections.Create();
        return await db.ExecuteAsync(sql, new
        {
            id = Guid.NewGuid(), channelId, eventKey, eventType, senderPhone, messageText, rawPayload
        }) == 1;
    }

    public async Task SetCallbackResultAsync(Guid channelId, string eventKey, bool delivered, string? error)
    {
        await using var db = connections.Create();
        await db.ExecuteAsync("""
            update webhook_events
               set callback_status = @status, callback_error = @error
             where channel_id = @channelId and event_key = @eventKey
            """, new { channelId, eventKey, status = delivered ? "delivered" : "failed", error });
    }
}
