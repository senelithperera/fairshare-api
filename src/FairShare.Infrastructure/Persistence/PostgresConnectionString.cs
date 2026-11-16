using Npgsql;

namespace FairShare.Infrastructure.Persistence;

/// <summary>
/// Accepts a PostgreSQL connection string in either form hosting providers hand out:
/// the Npgsql key/value form (<c>Host=…;Database=…</c>) or a URL
/// (<c>postgresql://user:pass@host:5432/db?sslmode=require</c>, as Neon, Render and Heroku give).
/// </summary>
public static class PostgresConnectionString
{
    /// <summary>
    /// Returns <paramref name="value"/> unchanged unless it is a <c>postgres://</c> or
    /// <c>postgresql://</c> URL, in which case it is converted to the Npgsql key/value form.
    /// Only <c>sslmode</c> is read from the query string; other parameters (such as Neon's
    /// <c>channel_binding</c>) are ignored.
    /// </summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        value = value.Trim();
        if (!value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            return value;

        var uri = new Uri(value);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host     = uri.Host,
            Port     = uri.Port > 0 ? uri.Port : 5432,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
        };

        var userInfo = uri.UserInfo.Split(':', 2);
        if (userInfo[0].Length > 0)
            builder.Username = Uri.UnescapeDataString(userInfo[0]);
        if (userInfo.Length > 1)
            builder.Password = Uri.UnescapeDataString(userInfo[1]);

        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 &&
                parts[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase) &&
                Enum.TryParse<SslMode>(parts[1].Replace("-", ""), ignoreCase: true, out var sslMode))
            {
                builder.SslMode = sslMode;
            }
        }

        return builder.ConnectionString;
    }
}
