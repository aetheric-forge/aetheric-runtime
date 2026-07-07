using System.Collections.Concurrent;

namespace AethericForge.Runtime.Institutions.Library;

public sealed class MemoryStore<TKey, TValue> : IStore<TKey, TValue>
    where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, TValue> _items = new();

    public Task SetAsync(TKey key, TValue value, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _items[key] = value;
        return Task.CompletedTask;
    }

    public Task<TValue?> GetAsync(TKey key, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_items.TryGetValue(key, out var value) ? value : default);
    }

    public Task<bool> ExistsAsync(TKey key, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_items.ContainsKey(key));
    }

    public Task<bool> RemoveAsync(TKey key, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_items.TryRemove(key, out _));
    }

    public Task ClearAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _items.Clear();
        return Task.CompletedTask;
    }
}
