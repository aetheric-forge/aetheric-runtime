using System.Text.Json;
using AethericForge.Runtime.SharedMemory.Abstractions;
using StackExchange.Redis;

namespace AethericForge.Runtime.SharedMemory;

public class RedisSharedMemory : ISharedMemory, IDisposable
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;

    public RedisSharedMemory(string connectionString)
    {
        var options = CreateConfigurationOptions(connectionString);
        _redis = ConnectionMultiplexer.Connect(options);
        _db = _redis.GetDatabase();
    }

    internal static ConfigurationOptions CreateConfigurationOptions(string connectionString)
    {
        ConfigurationOptions options;
        if (!Uri.TryCreate(connectionString, UriKind.Absolute, out var uri))
        {
            options = ConfigurationOptions.Parse(connectionString);
            options.AbortOnConnectFail = false;
            options.ConnectTimeout = 5000;
            options.SyncTimeout = 5000;
            return options;
        }

        var normalizedBuilder = new UriBuilder(uri) { Port = -1 };
        options = ConfigurationOptions.Parse(normalizedBuilder.Uri.ToString());
        options.AbortOnConnectFail = false;
        options.ConnectTimeout = 5000;
        options.SyncTimeout = 5000;
        options.Ssl = uri.Scheme.Equals("rediss", StringComparison.OrdinalIgnoreCase);
        options.EndPoints.Clear();
        options.EndPoints.Add(uri.Host, uri.IsDefaultPort ? 6379 : uri.Port);

        if (!string.IsNullOrWhiteSpace(uri.UserInfo))
        {
            var userInfo = Uri.UnescapeDataString(uri.UserInfo);
            var separatorIndex = userInfo.IndexOf(':');
            if (separatorIndex >= 0)
            {
                options.User = userInfo[..separatorIndex];
                options.Password = userInfo[(separatorIndex + 1)..];
            }
            else
            {
                options.User = userInfo;
            }
        }

        var databaseSegment = uri.AbsolutePath.Trim('/');
        if (!string.IsNullOrWhiteSpace(databaseSegment) && int.TryParse(databaseSegment, out var database))
        {
            options.DefaultDatabase = database;
        }

        return options;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var val = await _db.StringGetAsync(key);
        if (!val.HasValue) return default;

        var json = val.ToString();
        return JsonSerializer.Deserialize<T>(json);
    }

    public async Task SetAsync<T>(string key, T value, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(value);
        await _db.StringSetAsync(key, json);
    }

    public async Task<bool> RemoveAsync(string key, CancellationToken ct = default)
    {
        return await _db.KeyDeleteAsync(key);
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        return await _db.KeyExistsAsync(key);
    }

    public void Dispose()
    {
        _redis.Dispose();
    }
}
