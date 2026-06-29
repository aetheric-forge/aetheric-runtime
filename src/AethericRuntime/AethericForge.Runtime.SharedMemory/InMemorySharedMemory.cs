using System.Text.Json;
using AethericForge.Runtime.SharedMemory.Abstractions;

namespace AethericForge.Runtime.SharedMemory;

public class InMemorySharedMemory : ISharedMemory
{
    private readonly Dictionary<string, string> _dict = new();
    private readonly object _lock = new();

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        lock (_lock)
        {
            return Task.FromResult(
                _dict.TryGetValue(key, out var json) ? JsonSerializer.Deserialize<T>(json) : default
            );
        }
    }

    public Task SetAsync<T>(string key, T value, CancellationToken ct = default)
    {
        lock (_lock)
            _dict[key] = JsonSerializer.Serialize(value);
        return Task.CompletedTask;
    }

    public Task<bool> RemoveAsync(string key, CancellationToken ct = default)
    {
        lock (_lock)
            return Task.FromResult(_dict.Remove(key));
    }

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        lock (_lock)
            return Task.FromResult(_dict.ContainsKey(key));
    }
}
