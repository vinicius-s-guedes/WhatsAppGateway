using System.Text.Json.Serialization;

namespace WhatsAppGateway.Models;

public sealed class MetaWebhookEnvelope
{
    [JsonPropertyName("entry")] public List<MetaEntry> Entry { get; set; } = [];
}

public sealed class MetaEntry
{
    [JsonPropertyName("changes")] public List<MetaChange> Changes { get; set; } = [];
}

public sealed class MetaChange
{
    [JsonPropertyName("value")] public MetaValue Value { get; set; } = new();
}

public sealed class MetaValue
{
    [JsonPropertyName("metadata")] public MetaMetadata? Metadata { get; set; }
    [JsonPropertyName("contacts")] public List<MetaContact> Contacts { get; set; } = [];
    [JsonPropertyName("messages")] public List<MetaInboundMessage> Messages { get; set; } = [];
    [JsonPropertyName("statuses")] public List<MetaStatus> Statuses { get; set; } = [];
}

public sealed class MetaMetadata
{
    [JsonPropertyName("phone_number_id")] public string PhoneNumberId { get; set; } = "";
}

public sealed class MetaContact
{
    [JsonPropertyName("wa_id")] public string WaId { get; set; } = "";
    [JsonPropertyName("profile")] public MetaProfile? Profile { get; set; }
}

public sealed class MetaProfile
{
    [JsonPropertyName("name")] public string? Name { get; set; }
}

public sealed class MetaInboundMessage
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("from")] public string? From { get; set; }
    [JsonPropertyName("timestamp")] public string? Timestamp { get; set; }
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("text")] public MetaText? Text { get; set; }
}

public sealed class MetaText
{
    [JsonPropertyName("body")] public string? Body { get; set; }
}

public sealed class MetaStatus
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("timestamp")] public string? Timestamp { get; set; }
    [JsonPropertyName("recipient_id")] public string? RecipientId { get; set; }
}
