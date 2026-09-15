using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace WhatsAppGateway.Infrastructure;

public sealed class AdminApiKeyMiddleware(RequestDelegate next, IOptions<GatewayOptions> options)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api/admin") &&
            !context.Request.Path.StartsWithSegments("/api/messages"))
        {
            await next(context);
            return;
        }

        var expected = options.Value.AdminApiKey;
        var supplied = context.Request.Headers["X-Api-Key"].ToString();
        if (string.IsNullOrWhiteSpace(expected) || !FixedTimeEquals(expected, supplied))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "invalid_api_key" });
            return;
        }

        await next(context);
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length &&
               CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
