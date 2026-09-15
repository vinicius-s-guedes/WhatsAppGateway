using Dapper;

namespace WhatsAppGateway.Data;

public sealed class DatabaseInitializer(DbConnectionFactory connections)
{
    public async Task InitializeAsync()
    {
        await using var db = connections.Create();
        await db.OpenAsync();
        await db.ExecuteAsync(Sql);
    }

    private const string Sql = """
        create table if not exists tenants (
            id uuid primary key,
            name varchar(160) not null,
            callback_url text null,
            callback_secret_cipher text null,
            active boolean not null default true,
            created_at timestamptz not null default now()
        );

        create table if not exists whatsapp_apps (
            id uuid primary key,
            name varchar(160) not null,
            meta_app_id varchar(80) null,
            app_secret_cipher text not null,
            verify_token_cipher text not null,
            webhook_key uuid not null unique,
            active boolean not null default true,
            created_at timestamptz not null default now()
        );

        create table if not exists whatsapp_channels (
            id uuid primary key,
            tenant_id uuid not null references tenants(id) on delete cascade,
            app_id uuid not null references whatsapp_apps(id) on delete restrict,
            name varchar(160) not null,
            waba_id varchar(80) not null,
            phone_number_id varchar(80) not null unique,
            display_phone_number varchar(40) null,
            access_token_cipher text not null,
            graph_api_version varchar(16) not null,
            active boolean not null default true,
            created_at timestamptz not null default now()
        );

        create index if not exists ix_whatsapp_channels_tenant_id on whatsapp_channels(tenant_id);
        create index if not exists ix_whatsapp_channels_app_id on whatsapp_channels(app_id);

        create table if not exists webhook_events (
            id uuid primary key,
            channel_id uuid not null references whatsapp_channels(id) on delete cascade,
            event_key varchar(255) not null,
            event_type varchar(40) not null,
            sender_phone varchar(40) null,
            message_text text null,
            payload jsonb not null,
            callback_status varchar(30) not null default 'pending',
            callback_error text null,
            received_at timestamptz not null default now(),
            unique(channel_id, event_key)
        );
        """;
}
