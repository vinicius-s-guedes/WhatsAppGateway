using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using WhatsAppGateway.Data;
using WhatsAppGateway.Infrastructure;
using WhatsAppGateway.Services;

var builder = WebApplication.CreateBuilder(args);
DefaultTypeMap.MatchNamesWithUnderscores = true;

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.Configure<GatewayOptions>(builder.Configuration.GetSection("Gateway"));
builder.Services.AddSingleton<SecretProtector>();
builder.Services.AddSingleton<DbConnectionFactory>();
builder.Services.AddSingleton<DatabaseInitializer>();
builder.Services.AddScoped<GatewayRepository>();
builder.Services.AddScoped<WebhookService>();
builder.Services.AddHttpClient<MetaWhatsAppClient>(client => client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddHttpClient<TenantCallbackClient>(client => client.Timeout = TimeSpan.FromSeconds(5));
builder.Services.AddHealthChecks();

var app = builder.Build();

await app.Services.GetRequiredService<DatabaseInitializer>().InitializeAsync();

app.UseMiddleware<AdminApiKeyMiddleware>();
app.MapControllers();
app.MapHealthChecks("/healthz");

app.Run();

public partial class Program;
