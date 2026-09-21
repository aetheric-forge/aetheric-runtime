using System.Text.Json;
using Aetheric.Provisioning.Engine;
using StackExchange.Redis;

namespace Aetheric.Provisioning.Workbench.Redis;

/// <summary>Read-only check of an existing Workbench registration. Does not scan, claim,
/// probe-write, rename or adopt stages. Caller-owned IDatabase connections are never disposed.</summary>
public sealed class RedisWorkbenchParentCapabilityResolver : IParentCapabilityResolver, IDisposable
{
    private readonly WorkbenchParentLocation _location;
    private readonly ConfigurationOptions? _options;
    private IDatabase? _database;
    private ConnectionMultiplexer? _connection;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public RedisWorkbenchParentCapabilityResolver(IDatabase database, WorkbenchParentLocation location)
    { _database = database; _location = location; }

    // Connection is lazy: constructing a resolver during preflight performs no network I/O.
    public RedisWorkbenchParentCapabilityResolver(ConfigurationOptions options, WorkbenchParentLocation location)
    { _options = options.Clone(); _location = location; }

    public async Task<bool> IsAvailableAsync(ParentContext parent, string contract, string source, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (contract != "IWorkbench" || !parent.Capabilities.TryGetValue(contract, out var declared) || declared != source)
            return false;
        if (!WorkbenchParentLocation.TryParse(JsonSerializer.Serialize(_location), out _)) return false;
        await _gate.WaitAsync(ct);
        try
        {
            if (_database is null)
            {
                _connection = await ConnectionMultiplexer.ConnectAsync(_options!);
                _database = _connection.GetDatabase();
            }
            ct.ThrowIfCancellationRequested();
            // Read value and expiry atomically. An expiring claim cannot establish durable ownership.
            var result = await _database.ScriptEvaluateAsync("""
                if redis.call('PTTL', KEYS[1]) ~= -1 then return false end
                return redis.call('GET', KEYS[1])
                """, [RedisWorkbenchBackend.RegistrationKey(_location.Stage)]);
            ct.ThrowIfCancellationRequested();
            if (result.IsNull) return false;
            try
            {
                using var document = JsonDocument.Parse((string)result!);
                var root = document.RootElement;
                return root.GetProperty("Version").GetInt32() == 1
                    && root.GetProperty("Format").GetString() == "runtime-staging-hash-v1"
                    && root.GetProperty("Environment").GetString() == _location.Environment
                    && root.GetProperty("Institution").GetString() == _location.Institution
                    && root.GetProperty("Resource").GetString() == _location.Resource
                    && root.GetProperty("Stage").GetString() == _location.Stage;
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
            { return false; }
        }
        finally { _gate.Release(); }
    }

    public void Dispose() { _connection?.Dispose(); _gate.Dispose(); }
}
