using Npgsql;

namespace WhatsAppGateway.Data;

public sealed class DbConnectionFactory(IConfiguration configuration)
{
    public NpgsqlConnection Create()
    {
        var raw = Environment.GetEnvironmentVariable("DATABASE_URL")
                  ?? configuration.GetConnectionString("Default")
                  ?? throw new InvalidOperationException("DATABASE_URL or ConnectionStrings:Default is required.");
        return new NpgsqlConnection(Normalize(raw));
    }

    private static string Normalize(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "postgres" && uri.Scheme != "postgresql")) return value;

        var userInfo = uri.UserInfo.Split(':', 2);
        return new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = uri.AbsolutePath.TrimStart('/'),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "",
            SslMode = SslMode.Prefer
        }.ConnectionString;
    }
}
