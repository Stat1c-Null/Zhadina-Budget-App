using Npgsql;

namespace Zhadina.Api.Infrastructure;

public static class DatabaseConnection
{
    // Replit provides DATABASE_URL in URI form (postgres://user:pass@host:port/db?sslmode=require);
    // locally we use ConnectionStrings:Default in key=value form.
    public static string Resolve(IConfiguration config)
    {
        var raw = config["DATABASE_URL"] ?? config.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidOperationException(
                "No database configured. Set DATABASE_URL or ConnectionStrings:Default.");
        return ToNpgsql(raw);
    }

    public static string ToNpgsql(string raw)
    {
        if (!raw.StartsWith("postgres://") && !raw.StartsWith("postgresql://"))
            return raw;

        var uri = new Uri(raw);
        var userInfo = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
        };

        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split('=', 2);
            if (kv.Length == 2 && kv[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase)
                && Enum.TryParse<SslMode>(kv[1], ignoreCase: true, out var mode))
                builder.SslMode = mode;
        }

        return builder.ConnectionString;
    }
}
