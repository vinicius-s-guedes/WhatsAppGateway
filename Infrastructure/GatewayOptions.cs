namespace WhatsAppGateway.Infrastructure;

public sealed class GatewayOptions
{
    public string AdminApiKey { get; init; } = "";
    public string EncryptionKey { get; init; } = "";
    public string PublicBaseUrl { get; init; } = "http://localhost:8080";
    public string DefaultGraphApiVersion { get; init; } = "v23.0";
}
