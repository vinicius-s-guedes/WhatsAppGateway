namespace WhatsAppGateway.Models;

public sealed record TenantRecord(Guid Id, string Name, string? CallbackUrl, string? CallbackSecretCipher, bool Active, DateTimeOffset CreatedAt);

public sealed record WhatsAppAppRecord(
    Guid Id,
    string Name,
    string? MetaAppId,
    string AppSecretCipher,
    string VerifyTokenCipher,
    Guid WebhookKey,
    bool Active,
    DateTimeOffset CreatedAt);

public sealed record ChannelRecord(
    Guid Id,
    Guid TenantId,
    Guid AppId,
    string Name,
    string WabaId,
    string PhoneNumberId,
    string? DisplayPhoneNumber,
    string AccessTokenCipher,
    string GraphApiVersion,
    bool Active,
    DateTimeOffset CreatedAt);

public sealed record TenantCreateRequest(string Name, string? CallbackUrl, string? CallbackSecret);

public sealed record WhatsAppAppCreateRequest(string Name, string? MetaAppId, string AppSecret, string VerifyToken);

public sealed record ChannelCreateRequest(
    Guid AppId,
    string Name,
    string WabaId,
    string PhoneNumberId,
    string? DisplayPhoneNumber,
    string AccessToken,
    string? GraphApiVersion);

public sealed record TextMessageRequest(Guid TenantId, Guid ChannelId, string To, string Text);

public sealed record TemplateParameter(string Type, string? Text);

public sealed record TemplateMessageRequest(
    Guid TenantId,
    Guid ChannelId,
    string To,
    string TemplateName,
    string LanguageCode,
    IReadOnlyList<TemplateParameter>? BodyParameters);

public sealed record TemplateDocumentMessageRequest(
    Guid TenantId,
    Guid ChannelId,
    string To,
    string TemplateName,
    string LanguageCode,
    string? DocumentUrl,
    string? MediaId,
    string Filename,
    IReadOnlyList<TemplateParameter>? BodyParameters);

public sealed record InboundCallback(
    Guid TenantId,
    Guid ChannelId,
    string PhoneNumberId,
    string EventId,
    string EventType,
    string? From,
    string? CustomerName,
    string? MessageType,
    string? Text,
    DateTimeOffset ReceivedAt);
