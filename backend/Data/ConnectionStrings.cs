using Npgsql;
using StackExchange.Redis;

namespace backend.Data;

// Accepts either the library's own format or a URL (postgres://, redis://, rediss://),
// so connection strings from Coolify, Heroku, Render, etc. can be pasted as-is.
public static class ConnectionStrings
{
    // postgres://user:password@host:5432/database  ->  Host=host;Port=5432;Database=database;Username=user;Password=password
    public static string Postgres(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("ConnectionStrings:Postgres is not set.");

        if (!value.StartsWith("postgres://") && !value.StartsWith("postgresql://"))
            return value;

        var uri = new Uri(value);
        var (user, password) = SplitUserInfo(uri);

        return new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = uri.AbsolutePath.Trim('/'),
            Username = user,
            Password = password,
        }.ConnectionString;
    }

    // redis://default:password@host:6379/0  ->  host:6379,password=password,abortConnect=false
    // rediss:// (double s) turns on TLS. abortConnect=false is always applied.
    public static ConfigurationOptions Redis(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("ConnectionStrings:Redis is not set.");

        ConfigurationOptions options;

        if (value.StartsWith("redis://") || value.StartsWith("rediss://"))
        {
            var uri = new Uri(value);
            var (user, password) = SplitUserInfo(uri);

            options = new ConfigurationOptions { Ssl = uri.Scheme == "rediss", Password = password };
            options.EndPoints.Add(uri.Host, uri.Port > 0 ? uri.Port : 6379);

            if (!string.IsNullOrEmpty(user) && user != "default")
                options.User = user;

            if (int.TryParse(uri.AbsolutePath.Trim('/'), out int db))
                options.DefaultDatabase = db;
        }
        else
        {
            options = ConfigurationOptions.Parse(value);
        }

        options.AbortOnConnectFail = false;
        return options;
    }

    private static (string? User, string? Password) SplitUserInfo(Uri uri)
    {
        if (string.IsNullOrEmpty(uri.UserInfo))
            return (null, null);

        string[] parts = uri.UserInfo.Split(':', 2);
        return (Uri.UnescapeDataString(parts[0]),
                parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : null);
    }
}
